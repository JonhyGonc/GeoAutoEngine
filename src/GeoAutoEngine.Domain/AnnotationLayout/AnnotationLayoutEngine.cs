using System.Diagnostics;

namespace GeoAutoEngine.Domain.AnnotationLayout;

public sealed class AnnotationLayoutEngine
{
    private readonly AnnotationLayoutOptions _options;
    private readonly ITextMetricsProvider _metricsProvider;

    public AnnotationLayoutEngine(
        AnnotationLayoutOptions? options = null,
        ITextMetricsProvider? metricsProvider = null)
    {
        _options = options ?? new AnnotationLayoutOptions();
        _metricsProvider = metricsProvider ?? new ApproximateTextMetricsProvider();
    }

    public AnnotationLayoutBatchResult Layout(
        IEnumerable<AnnotationLayoutRequest> requests,
        LayoutScene scene)
    {
        ArgumentNullException.ThrowIfNull(requests);
        ArgumentNullException.ThrowIfNull(scene);
        var stopwatch = Stopwatch.StartNew();
        var ordered = requests
            .OrderBy(request => request.AnnotationId, StringComparer.Ordinal)
            .ThenBy(request => request.Anchor.X)
            .ThenBy(request => request.Anchor.Y)
            .ToArray();
        var obstacleIndex = CreateObstacleIndex(scene.Obstacles);
        var networkIndex = new NetworkSegmentIndex(
            scene.Networks,
            _options.SpatialCellSize,
            _options.MaximumIndexedCellsPerItem);
        var states = ordered.Select(request => Prepare(request, networkIndex)).ToArray();
        var decisions = _options.Strategy == AnnotationLayoutStrategy.HybridSimple
            ? LayoutHybrid(states, obstacleIndex, networkIndex)
            : LayoutComparison(states, obstacleIndex, networkIndex, _options.Strategy);
        stopwatch.Stop();
        return Result(decisions, obstacleIndex.QueryCount + networkIndex.QueryCount, stopwatch.ElapsedMilliseconds);
    }

    private IReadOnlyList<LayoutDecision> LayoutHybrid(
        IReadOnlyList<PreparedAnnotation> states,
        SpatialIndex<LayoutObstacle> obstacleIndex,
        NetworkSegmentIndex networkIndex)
    {
        var decisions = new Dictionary<string, LayoutDecision>(StringComparer.Ordinal);
        var pending = new List<(PreparedAnnotation State, CandidateEvaluation Legacy)>();

        // Pass 1 fixes every healthy existing/legacy placement before any correction is allowed.
        // Consequently, correcting C cannot cause healthy D/E annotations to move later.
        foreach (var state in states)
        {
            var legacy = Evaluate(state, state.Legacy, obstacleIndex, networkIndex);
            if (state.Existing is not null)
            {
                var existing = Evaluate(state, state.Existing, obstacleIndex, networkIndex);
                if (existing.Validation.IsValid)
                {
                    var decision = Decision(
                        state,
                        existing,
                        legacy.Validation,
                        LayoutDecisionKind.KeepExisting,
                        evaluatedCandidateCount: 2,
                        reasonForMove: null);
                    decisions.Add(state.Request.AnnotationId, decision);
                    AddDecisionObstacles(state, decision, obstacleIndex);
                    continue;
                }
            }

            if (legacy.Validation.IsValid)
            {
                var decision = Decision(
                    state,
                    legacy,
                    legacy.Validation,
                    LayoutDecisionKind.KeepLegacy,
                    evaluatedCandidateCount: state.Existing is null ? 1 : 2,
                    reasonForMove: null);
                decisions.Add(state.Request.AnnotationId, decision);
                AddDecisionObstacles(state, decision, obstacleIndex);
                continue;
            }

            pending.Add((state, legacy));
        }

        // Pass 2 considers only objectively invalid baselines and stops at the first tier that
        // offers a valid solution. Score never overrides movement locality.
        foreach (var item in pending)
        {
            var evaluated = item.State.Existing is null ? 1 : 2;
            CandidateEvaluation? selected = null;
            foreach (var tier in GenerateTieredCandidates(item.State).GroupBy(candidate => candidate.Tier).OrderBy(group => group.Key))
            {
                var valid = new List<CandidateEvaluation>();
                foreach (var candidate in tier)
                {
                    var evaluation = Evaluate(item.State, candidate, obstacleIndex, networkIndex);
                    evaluated++;
                    if (evaluation.Validation.IsValid)
                    {
                        valid.Add(evaluation);
                    }
                }

                selected = valid.OrderBy(evaluation => evaluation.Candidate.MovementDistance)
                    .ThenBy(evaluation => evaluation.Candidate.SideChanged)
                    .ThenBy(evaluation => Math.Abs(evaluation.Candidate.TangentialOffset))
                    .ThenBy(evaluation => Math.Abs(evaluation.Candidate.NormalOffset))
                    .ThenByDescending(evaluation => evaluation.Validation.MinimumClearance)
                    .ThenByDescending(evaluation => evaluation.Score.Total)
                    .ThenBy(evaluation => evaluation.Candidate.Id, StringComparer.Ordinal)
                    .FirstOrDefault();
                if (selected is not null)
                {
                    break;
                }
            }

            var decision = selected is null
                ? Decision(
                    item.State,
                    item.Legacy,
                    item.Legacy.Validation,
                    LayoutDecisionKind.RequiresReview,
                    evaluated,
                    "Nenhum candidato padrão dentro do MovementBudget eliminou os conflitos Legacy.")
                : Decision(
                    item.State,
                    selected,
                    item.Legacy.Validation,
                    LayoutDecisionKind.Corrected,
                    evaluated,
                    string.Join("; ", item.Legacy.Validation.Details));
            decisions.Add(item.State.Request.AnnotationId, decision);
            AddDecisionObstacles(item.State, decision, obstacleIndex);
        }

        return states.Select(state => decisions[state.Request.AnnotationId]).ToArray();
    }

    private IReadOnlyList<LayoutDecision> LayoutComparison(
        IReadOnlyList<PreparedAnnotation> states,
        SpatialIndex<LayoutObstacle> obstacleIndex,
        NetworkSegmentIndex networkIndex,
        AnnotationLayoutStrategy strategy)
    {
        var decisions = new List<LayoutDecision>(states.Count);
        foreach (var state in states)
        {
            var legacy = Evaluate(state, state.Legacy, obstacleIndex, networkIndex);
            if (strategy == AnnotationLayoutStrategy.LegacyOnly)
            {
                var legacyDecision = Decision(
                    state,
                    legacy,
                    legacy.Validation,
                    legacy.Validation.IsValid ? LayoutDecisionKind.KeepLegacy : LayoutDecisionKind.RequiresReview,
                    1,
                    legacy.Validation.IsValid ? null : string.Join("; ", legacy.Validation.Details));
                decisions.Add(legacyDecision);
                AddDecisionObstacles(state, legacyDecision, obstacleIndex);
                continue;
            }

            CandidateEvaluation? best = null;
            var evaluated = 0;
            foreach (var candidate in GeneratePhaseACandidates(state))
            {
                var evaluation = Evaluate(state, candidate, obstacleIndex, networkIndex);
                evaluated++;
                if (best is null || BetterByPhaseAScore(evaluation, best))
                {
                    best = evaluation;
                }

                if (evaluation.Validation.IsValid && evaluation.Score.NetworkCrossingCount == 0 &&
                    Math.Abs(evaluation.Score.ObstaclePenalty) <= LayoutGeometryTolerance.Score &&
                    (candidate.Kind is CandidateKind.Existing or CandidateKind.PhaseAPreferred))
                {
                    best = evaluation;
                    break;
                }
            }

            best ??= legacy;
            var sameAsLegacy = SamePlacements(best.Candidate.Placements, state.Legacy.Placements);
            var sameAsExisting = state.Existing is not null && SamePlacements(best.Candidate.Placements, state.Existing.Placements);
            var kind = !best.Validation.IsValid || state.Association.IsFallback
                ? LayoutDecisionKind.RequiresReview
                : sameAsExisting
                    ? LayoutDecisionKind.KeepExisting
                    : sameAsLegacy
                        ? LayoutDecisionKind.KeepLegacy
                        : LayoutDecisionKind.Corrected;
            var decision = Decision(
                state,
                best,
                legacy.Validation,
                kind,
                evaluated,
                kind == LayoutDecisionKind.Corrected ? "Phase A free-score candidate won the global score comparison." : null,
                unnecessaryMove: legacy.Validation.IsValid && !sameAsLegacy && !sameAsExisting);
            decisions.Add(decision);
            AddDecisionObstacles(state, decision, obstacleIndex);
        }

        return decisions;
    }

    private PreparedAnnotation Prepare(AnnotationLayoutRequest request, NetworkSegmentIndex networkIndex)
    {
        var association = networkIndex.Associate(request, _options) ?? NetworkAssociation.HorizontalFallback(request.Anchor);
        var syntheticLegacy = CandidateFromAssociation(request, association, "legacy:synthetic") with
        {
            Kind = CandidateKind.Legacy,
        };
        var legacy = request.LegacyLayout is null
            ? syntheticLegacy
            : CandidateFromLegacy(request.LegacyLayout, syntheticLegacy, association);
        var existing = request.PreviousLayout is not null && PreviousMatchesRequest(request)
            ? CandidateFromExisting(request.PreviousLayout, legacy, association)
            : null;
        return new PreparedAnnotation(request, association, legacy, existing, Budget(request));
    }

    private MovementBudget Budget(AnnotationLayoutRequest request)
    {
        var height = request.Texts.Max(text => text.Height);
        var maximumFootprintDimension = request.Texts.Select(text => _metricsProvider.Measure(text))
            .Select(metrics => Math.Max(metrics.Width, metrics.Height))
            .DefaultIfEmpty(height)
            .Max();
        var preferred = Math.Max(height * _options.PreferredAdjustmentFactor, _options.Clearance);
        var heightLimit = height * _options.MaximumStandardAdjustmentFactor;
        var footprintLimit = Math.Min(maximumFootprintDimension, heightLimit);
        var maximum = Math.Max(
            preferred * 2,
            Math.Max(_options.Clearance * 4, footprintLimit));
        return new MovementBudget(preferred, maximum, maximum * _options.ExistingMovementBudgetMultiplier);
    }

    private IEnumerable<LayoutCandidate> GenerateTieredCandidates(PreparedAnnotation state)
    {
        var preferred = state.Budget.PreferredAdjustment;
        var maximum = state.Budget.MaximumStandardAdjustment;
        var values = new[]
        {
            (Tier: 1, Scale: 1.0),
            (Tier: 2, Scale: 2.0),
            (Tier: 3, Scale: 3.0),
        };
        var seen = new HashSet<(long Tangential, long Normal)>();
        foreach (var value in values)
        {
            var amount = Math.Min(maximum, preferred * value.Scale);
            var offsets = new (double Tangential, double Normal)[]
            {
                (amount, 0), (-amount, 0),
                (0, amount), (0, -amount),
            };
            foreach (var offset in offsets)
            {
                var distance = Math.Sqrt((offset.Tangential * offset.Tangential) + (offset.Normal * offset.Normal));
                if (distance > maximum + LayoutGeometryTolerance.Distance)
                {
                    continue;
                }

                var key = (
                    (long)Math.Round(offset.Tangential / LayoutGeometryTolerance.Distance),
                    (long)Math.Round(offset.Normal / LayoutGeometryTolerance.Distance));
                if (!seen.Add(key))
                {
                    continue;
                }

                var candidate = ShiftLegacy(state, offset.Tangential, offset.Normal, value.Tier);
                var invariants = AnnotationGroupInvariantValidator.Validate(
                    state.Request.LegacyLayout?.Anchor ?? state.Request.Anchor,
                    state.Legacy.Placements,
                    candidate.Placements);
                if (candidate.SideChanged || !invariants.IsValid)
                {
                    continue;
                }

                yield return candidate;
            }
        }
    }

    private IEnumerable<LayoutCandidate> GeneratePhaseACandidates(PreparedAnnotation state)
    {
        if (state.Existing is not null)
        {
            yield return state.Existing;
        }

        yield return CandidateFromAssociation(state.Request, state.Association, "phase-a:preferred") with
        {
            Kind = CandidateKind.PhaseAPreferred,
        };
        var height = state.Request.Texts.Max(text => text.Height);
        var offsets = new (double Tangential, double Normal)[]
        {
            (1, 0), (-1, 0), (2, 0), (-2, 0), (4, 0), (-4, 0),
            (0, 1.5), (0, -1.5), (0, 3), (0, -3),
            (1, 1.5), (-1, 1.5), (2, 1.5), (-2, 1.5),
            (1, -1.5), (-1, -1.5), (2, -1.5), (-2, -1.5),
            (1, 3), (-1, 3), (1, -3), (-1, -3),
        };
        for (var index = 0; index < offsets.Length; index++)
        {
            var tangential = offsets[index].Tangential * height;
            var normal = offsets[index].Normal * height;
            if (Math.Sqrt((tangential * tangential) + (normal * normal)) <=
                _options.MaximumDisplacement + LayoutGeometryTolerance.Distance)
            {
                yield return CandidateFromAssociation(
                    state.Request,
                    state.Association,
                    $"phase-a:standard-{index + 1:00}",
                    tangential,
                    normal);
            }
        }
    }

    private CandidateEvaluation Evaluate(
        PreparedAnnotation state,
        LayoutCandidate candidate,
        SpatialIndex<LayoutObstacle> obstacleIndex,
        NetworkSegmentIndex networkIndex)
    {
        var issues = LegacyIssue.None;
        var details = new List<string>();
        var reasons = new List<string>();
        var groupInvariants = AnnotationGroupInvariantValidator.Validate(
            state.Request.LegacyLayout?.Anchor ?? state.Request.Anchor,
            state.Legacy.Placements,
            candidate.Placements);
        issues |= InvariantIssues(groupInvariants.Violations);
        details.AddRange(groupInvariants.Details);
        reasons.AddRange(groupInvariants.Details.Select(detail => $"HARD:{detail}"));
        var owned = new HashSet<string>(state.Request.Texts.Select(text => text.ItemId), StringComparer.Ordinal);
        var textHeight = state.Request.Texts.Max(text => text.Height);
        var tolerance = textHeight * _options.VisualContactToleranceFactor;
        if (candidate.Placements.Any(placement => placement.Footprint.Approximate))
        {
            tolerance = Math.Max(tolerance, textHeight * _options.ApproximateContactToleranceFactor);
        }
        var groupBounds = candidate.Placements.Select(item => item.Footprint.Rectangle.Bounds)
            .Aggregate((left, right) => left.Union(right));
        var minimumClearance = double.PositiveInfinity;
        var softPenalty = 0.0;

        foreach (var obstacle in obstacleIndex.Query(groupBounds.Expand(_options.Clearance + _options.SpatialCellSize)))
        {
            if (obstacle.OwnerItemId is not null && owned.Contains(obstacle.OwnerItemId))
            {
                continue;
            }

            var distance = candidate.Placements.Min(placement =>
                placement.Footprint.Rectangle.Bounds.DistanceTo(obstacle.Footprint.Bounds));
            minimumClearance = Math.Min(minimumClearance, distance);
            var collision = candidate.Placements.Any(placement =>
            {
                var footprint = placement.Footprint.Approximate && tolerance > 0
                    ? placement.Footprint.Rectangle.Inset(tolerance)
                    : placement.Footprint.Rectangle;
                return footprint.Intersects(obstacle.Footprint);
            });
            if (collision)
            {
                var issue = Issue(obstacle.Kind);
                issues |= issue;
                var source = obstacle.MeasurementSource == LayoutObstacleMeasurementSource.Unknown
                    ? string.Empty
                    : $" source={obstacle.MeasurementSource}";
                var detail = $"{issue} with {obstacle.Id}{source}";
                details.Add(detail);
                reasons.Add($"HARD:{detail}");
                continue;
            }

            if (obstacle.Kind is LayoutObstacleKind.ExistingText or LayoutObstacleKind.PlannedText or
                LayoutObstacleKind.Block or LayoutObstacleKind.Structure &&
                distance + tolerance < _options.Clearance - LayoutGeometryTolerance.Distance)
            {
                var detail = $"InsufficientClearance {distance:F4}m from {obstacle.Id} " +
                    $"(required {_options.Clearance:F4}m, tolerance {tolerance:F4}m, source {obstacle.MeasurementSource})";
                reasons.Add($"SOFT:{detail}");
            }
            else if (obstacle.Kind == LayoutObstacleKind.Other &&
                candidate.Placements.Any(placement => placement.Footprint.Rectangle.Expand(_options.Clearance)
                    .Intersects(obstacle.Footprint)))
            {
                softPenalty -= _options.OtherObstaclePenalty;
                reasons.Add($"SOFT:{obstacle.Kind}:{obstacle.Id}");
            }
        }

        for (var first = 0; first < candidate.Placements.Count; first++)
        {
            for (var second = first + 1; second < candidate.Placements.Count; second++)
            {
                if (candidate.Placements[first].Footprint.Rectangle.Inset(tolerance)
                    .Intersects(candidate.Placements[second].Footprint.Rectangle.Inset(tolerance)))
                {
                    issues |= LegacyIssue.DepthDiameterCollision;
                    details.Add("DepthDiameterCollision within the annotation group");
                    reasons.Add("HARD:DepthDiameterCollision");
                }
            }
        }

        if (candidate.Placements.Any(placement =>
            Math.Abs(placement.RotationRadians) > (Math.PI / 2) + LayoutGeometryTolerance.Angle))
        {
            issues |= LegacyIssue.InvalidRotation;
            details.Add("InvalidRotation");
            reasons.Add("HARD:InvalidRotation");
        }

        var maximumDistance = candidate.Kind switch
        {
            CandidateKind.Existing => double.PositiveInfinity,
            CandidateKind.Legacy => _options.MaximumDisplacement,
            _ => state.Budget.MaximumStandardAdjustment,
        };
        var measuredDistance = candidate.Kind == CandidateKind.Legacy
            ? candidate.DistanceFromPreferred
            : candidate.MovementDistance;
        if (measuredDistance > maximumDistance + LayoutGeometryTolerance.Distance)
        {
            issues |= LegacyIssue.ExcessiveDistance;
            details.Add($"ExcessiveDistance {measuredDistance:F4}m > {maximumDistance:F4}m");
            reasons.Add("HARD:ExcessiveDistance");
        }

        if (state.Association.IsFallback)
        {
            issues |= LegacyIssue.InvalidGeometry;
            details.Add("InvalidGeometry: associated network not found");
            reasons.Add("HARD:InvalidGeometry");
        }

        var associatedPaths = new HashSet<string>(StringComparer.Ordinal);
        var otherPaths = new HashSet<string>(StringComparer.Ordinal);
        foreach (var segment in networkIndex.Query(groupBounds.Expand(_options.Clearance)))
        {
            if (!candidate.Placements.Any(placement =>
                placement.Footprint.Rectangle.Expand(_options.Clearance).IntersectsSegment(segment.Start, segment.End)))
            {
                continue;
            }

            if (segment.PathId == state.Association.PathId)
            {
                associatedPaths.Add(segment.PathId);
            }
            else
            {
                otherPaths.Add(segment.PathId);
            }
        }

        if (associatedPaths.Count > 0)
        {
            reasons.Add($"NETWORK:ASSOCIATED:{associatedPaths.Count}");
        }

        if (otherPaths.Count > 0)
        {
            reasons.Add($"NETWORK:OTHER:{otherPaths.Count}");
        }

        var distanceScore = -(candidate.MovementDistance * _options.DistanceWeight);
        var preferredScore = candidate.Kind is CandidateKind.Legacy or CandidateKind.PhaseAPreferred
            ? _options.PreferredPositionReward
            : 0;
        var stabilityScore = candidate.Kind == CandidateKind.Existing ? _options.StabilityReward : 0;
        var sideScore = candidate.SideChanged ? -_options.OppositeSidePenalty : 0;
        var networkScore = -(associatedPaths.Count * _options.AssociatedNetworkCrossingPenalty) -
            (otherPaths.Count * _options.OtherNetworkCrossingPenalty);
        var clearanceScore = double.IsPositiveInfinity(minimumClearance)
            ? _options.SpatialCellSize * _options.ClearanceRewardWeight
            : Math.Min(_options.SpatialCellSize, minimumClearance) * _options.ClearanceRewardWeight;
        var score = new LayoutScore(
            distanceScore,
            preferredScore,
            stabilityScore,
            sideScore,
            networkScore,
            clearanceScore,
            softPenalty,
            CountIssues(issues),
            associatedPaths.Count + otherPaths.Count,
            reasons);
        return new CandidateEvaluation(
            candidate,
            new LegacyValidationResult(issues, minimumClearance, details.Distinct(StringComparer.Ordinal)),
            score,
            groupInvariants);
    }

    private LayoutDecision Decision(
        PreparedAnnotation state,
        CandidateEvaluation selected,
        LegacyValidationResult legacyValidation,
        LayoutDecisionKind kind,
        int evaluatedCandidateCount,
        string? reasonForMove,
        bool unnecessaryMove = false)
    {
        var diagnostics = new List<string>();
        if (state.Association.IsFallback)
        {
            diagnostics.Add("ANNOTATION_LAYOUT_NETWORK_NOT_FOUND");
        }

        if (kind == LayoutDecisionKind.RequiresReview)
        {
            diagnostics.Add("ANNOTATION_LAYOUT_UNRESOLVED");
        }

        if (unnecessaryMove)
        {
            diagnostics.Add("ANNOTATION_LAYOUT_UNNECESSARY_MOVE");
        }

        foreach (var invariant in selected.GroupInvariants.Details)
        {
            diagnostics.Add($"ANNOTATION_LAYOUT_{invariant.Split(':')[0].ToUpperInvariant()}");
        }

        if (selected.Candidate.Placements.Any(placement => placement.Footprint.Approximate))
        {
            diagnostics.Add("ANNOTATION_LAYOUT_APPROXIMATE_TEXT_METRICS");
        }

        var quality = kind == LayoutDecisionKind.RequiresReview
            ? CorrectionQuality.Review
            : selected.Candidate.MovementDistance <= state.Budget.PreferredAdjustment + LayoutGeometryTolerance.Distance
                ? CorrectionQuality.Excellent
                : selected.Candidate.MovementDistance <= state.Budget.PreferredAdjustment * 2 + LayoutGeometryTolerance.Distance
                    ? CorrectionQuality.Good
                    : CorrectionQuality.Acceptable;
        return new LayoutDecision(
            state.Request.AnnotationId,
            kind == LayoutDecisionKind.RequiresReview ? LayoutStatus.RequiresReview : LayoutStatus.Resolved,
            LayoutMode.Standard,
            selected.Candidate.Id,
            selected.Candidate.Placements,
            selected.Score,
            state.Association.IsFallback ? null : state.Association.SemanticId,
            state.Association.IsFallback ? null : state.Association.Layer,
            evaluatedCandidateCount,
            diagnostics,
            kind,
            legacyValidation,
            selected.Candidate.MovementDistance,
            selected.Candidate.SideChanged,
            quality,
            reasonForMove,
            state.Legacy.Placements,
            selected.GroupInvariants);
    }

    private AnnotationLayoutBatchResult Result(
        IReadOnlyList<LayoutDecision> decisions,
        long spatialQueries,
        long elapsedMilliseconds)
    {
        var corrections = decisions.Where(decision => decision.DecisionKind == LayoutDecisionKind.Corrected).ToArray();
        var statistics = new AnnotationLayoutStatistics(
            decisions.Count,
            decisions.Sum(decision => decision.EvaluatedCandidateCount),
            spatialQueries,
            decisions.Count(decision => decision.DecisionKind == LayoutDecisionKind.RequiresReview),
            elapsedMilliseconds,
            decisions.Count(decision => decision.LegacyValidation.IsValid),
            decisions.Count(decision => !decision.LegacyValidation.IsValid),
            decisions.Count(decision => decision.DecisionKind == LayoutDecisionKind.KeepExisting),
            decisions.Count(decision => decision.DecisionKind == LayoutDecisionKind.KeepLegacy),
            corrections.Length,
            decisions.Count(decision => decision.Diagnostics.Contains("ANNOTATION_LAYOUT_UNNECESSARY_MOVE", StringComparer.Ordinal)),
            corrections.Length == 0 ? 0 : corrections.Average(decision => decision.MovementDistance),
            corrections.Length == 0 ? 0 : corrections.Max(decision => decision.MovementDistance),
            corrections.Count(decision => decision.SideChanged),
            corrections.Count(decision => decision.MovementDistance <= 0.25 + LayoutGeometryTolerance.Distance),
            corrections.Count(decision => decision.MovementDistance > 0.25 + LayoutGeometryTolerance.Distance &&
                decision.MovementDistance <= 0.50 + LayoutGeometryTolerance.Distance),
            corrections.Count(decision => decision.MovementDistance > 0.50 + LayoutGeometryTolerance.Distance &&
                decision.MovementDistance <= 0.75 + LayoutGeometryTolerance.Distance),
            corrections.Count(decision => decision.MovementDistance > 0.75 + LayoutGeometryTolerance.Distance),
            decisions.Count(decision => decision.GroupInvariants.Violations.HasFlag(AnnotationGroupInvariantViolation.Order)),
            decisions.Count(decision => decision.GroupInvariants.Violations.HasFlag(AnnotationGroupInvariantViolation.Rotation)),
            decisions.Count(decision => decision.GroupInvariants.Violations.HasFlag(AnnotationGroupInvariantViolation.Side)),
            decisions.Count(decision => decision.GroupInvariants.Violations.HasFlag(AnnotationGroupInvariantViolation.GroupIntegrity)));
        return new AnnotationLayoutBatchResult(decisions, statistics);
    }

    private LayoutCandidate CandidateFromAssociation(
        AnnotationLayoutRequest request,
        NetworkAssociation association,
        string id,
        double tangentialOffset = 0,
        double normalOffset = 0)
    {
        var tangent = association.Tangent;
        var normal = new LayoutVector2(-tangent.Y, tangent.X);
        var translation = (tangent * tangentialOffset) + (normal * normalOffset);
        var rotation = LayoutAngles.NormalizeReadable(Math.Atan2(tangent.Y, tangent.X));
        var placements = request.Texts.Select(text =>
        {
            var position = association.ClosestPoint + translation + (normal * text.PerpendicularOffset);
            var footprint = TextFootprint.Create(text, position, rotation, _metricsProvider.Measure(text));
            return new LayoutTextPlacement(text.ItemId, text.Role, position, rotation, footprint);
        }).ToArray();
        var movement = Math.Sqrt((tangentialOffset * tangentialOffset) + (normalOffset * normalOffset));
        return new LayoutCandidate(
            id,
            CandidateKind.Adjustment,
            0,
            tangentialOffset,
            normalOffset,
            movement,
            movement,
            SideChanged: false,
            placements);
    }

    private static LayoutCandidate CandidateFromLegacy(
        LegacyAnnotationLayout legacy,
        LayoutCandidate syntheticLegacy,
        NetworkAssociation association)
    {
        var movement = MaximumPlacementDistance(legacy.Placements, syntheticLegacy.Placements);
        var offsets = OffsetFromReference(legacy.Placements, syntheticLegacy.Placements, association);
        return new LayoutCandidate(
            $"legacy:{legacy.CandidateId}",
            CandidateKind.Legacy,
            0,
            offsets.Tangential,
            offsets.Normal,
            0,
            movement,
            SideChanged: false,
            legacy.Placements);
    }

    private static LayoutCandidate CandidateFromExisting(
        PreviousAnnotationLayout existing,
        LayoutCandidate legacy,
        NetworkAssociation association)
    {
        var movement = MaximumPlacementDistance(existing.Placements, legacy.Placements);
        var offsets = OffsetFromPreferred(existing.Placements, association);
        return new LayoutCandidate(
            $"existing:{existing.CandidateId}",
            CandidateKind.Existing,
            0,
            offsets.Tangential,
            offsets.Normal,
            movement,
            offsets.Distance,
            SideChanged(legacy.Placements, existing.Placements, association),
            existing.Placements);
    }

    private static LayoutCandidate ShiftLegacy(
        PreparedAnnotation state,
        double tangential,
        double normal,
        int tier)
    {
        var translation = (state.Association.Tangent * tangential) +
            (new LayoutVector2(-state.Association.Tangent.Y, state.Association.Tangent.X) * normal);
        var placements = state.Legacy.Placements.Select(placement =>
        {
            var position = placement.Position + translation;
            var rectangle = placement.Footprint.Rectangle;
            var footprint = new TextFootprint(
                placement.ItemId,
                new OrientedRect(rectangle.Center + translation, rectangle.HalfWidth, rectangle.HalfHeight, rectangle.RotationRadians),
                placement.Footprint.Approximate);
            return new LayoutTextPlacement(
                placement.ItemId,
                placement.Role,
                position,
                placement.RotationRadians,
                footprint);
        }).ToArray();
        var distance = Math.Sqrt((tangential * tangential) + (normal * normal));
        return new LayoutCandidate(
            $"tier-{tier}:t={tangential:R}:n={normal:R}",
            CandidateKind.Adjustment,
            tier,
            tangential,
            normal,
            distance,
            state.Legacy.DistanceFromPreferred + distance,
            SideChanged(state.Legacy.Placements, placements, state.Association),
            placements);
    }

    private static (double Tangential, double Normal, double Distance) OffsetFromPreferred(
        IReadOnlyList<LayoutTextPlacement> placements,
        NetworkAssociation association)
    {
        var first = placements.First();
        var delta = first.Position - association.ClosestPoint;
        var normal = new LayoutVector2(-association.Tangent.Y, association.Tangent.X);
        var tangential = LayoutVector2.Dot(delta, association.Tangent);
        var normalValue = LayoutVector2.Dot(delta, normal);
        return (tangential, normalValue, Math.Sqrt((tangential * tangential) + (normalValue * normalValue)));
    }

    private static (double Tangential, double Normal, double Distance) OffsetFromReference(
        IReadOnlyList<LayoutTextPlacement> placements,
        IReadOnlyList<LayoutTextPlacement> reference,
        NetworkAssociation association)
    {
        var referenceById = reference.ToDictionary(item => item.ItemId, StringComparer.Ordinal);
        var first = placements.First();
        var referencePlacement = referenceById.TryGetValue(first.ItemId, out var matching)
            ? matching
            : reference.First();
        var delta = first.Position - referencePlacement.Position;
        var normal = new LayoutVector2(-association.Tangent.Y, association.Tangent.X);
        var tangential = LayoutVector2.Dot(delta, association.Tangent);
        var normalValue = LayoutVector2.Dot(delta, normal);
        return (tangential, normalValue, Math.Sqrt((tangential * tangential) + (normalValue * normalValue)));
    }

    private static bool SideChanged(
        IReadOnlyList<LayoutTextPlacement> legacy,
        IReadOnlyList<LayoutTextPlacement> candidate,
        NetworkAssociation association)
    {
        var normal = new LayoutVector2(-association.Tangent.Y, association.Tangent.X);
        var legacyCenter = Center(legacy);
        var candidateCenter = Center(candidate);
        var legacySide = LayoutVector2.Dot(legacyCenter - association.ClosestPoint, normal);
        var candidateSide = LayoutVector2.Dot(candidateCenter - association.ClosestPoint, normal);
        return Math.Abs(legacySide) > LayoutGeometryTolerance.Distance &&
            Math.Abs(candidateSide) > LayoutGeometryTolerance.Distance &&
            Math.Sign(legacySide) != Math.Sign(candidateSide);
    }

    private static LayoutPoint2 Center(IReadOnlyList<LayoutTextPlacement> placements) =>
        new(placements.Average(item => item.Footprint.Rectangle.Center.X), placements.Average(item => item.Footprint.Rectangle.Center.Y));

    private static double MaximumPlacementDistance(
        IReadOnlyList<LayoutTextPlacement> first,
        IReadOnlyList<LayoutTextPlacement> second)
    {
        var byId = second.ToDictionary(item => item.ItemId, StringComparer.Ordinal);
        return first.Max(item => (item.Position - byId[item.ItemId].Position).Length);
    }

    private static LegacyIssue Issue(LayoutObstacleKind kind) => kind switch
    {
        LayoutObstacleKind.ExistingText or LayoutObstacleKind.PlannedText => LegacyIssue.TextCollision,
        LayoutObstacleKind.Block => LegacyIssue.BlockCollision,
        LayoutObstacleKind.Structure => LegacyIssue.StructureCollision,
        _ => LegacyIssue.OtherHardCollision,
    };

    private static LegacyIssue InvariantIssues(AnnotationGroupInvariantViolation violations)
    {
        var issues = LegacyIssue.None;
        if (violations.HasFlag(AnnotationGroupInvariantViolation.Order))
        {
            issues |= LegacyIssue.OrderInvariantViolation;
        }

        if (violations.HasFlag(AnnotationGroupInvariantViolation.Rotation))
        {
            issues |= LegacyIssue.RotationInvariantViolation;
        }

        if (violations.HasFlag(AnnotationGroupInvariantViolation.Side))
        {
            issues |= LegacyIssue.SideInvariantViolation;
        }

        if (violations.HasFlag(AnnotationGroupInvariantViolation.GroupIntegrity))
        {
            issues |= LegacyIssue.GroupIntegrityViolation;
        }

        return issues;
    }

    private static int CountIssues(LegacyIssue issues)
    {
        var value = (int)issues;
        var count = 0;
        while (value != 0)
        {
            count += value & 1;
            value >>= 1;
        }

        return count;
    }

    private static bool BetterByPhaseAScore(CandidateEvaluation candidate, CandidateEvaluation current)
    {
        if (candidate.Score.IsValid != current.Score.IsValid)
        {
            return candidate.Score.IsValid;
        }

        var delta = candidate.Score.Total - current.Score.Total;
        return Math.Abs(delta) > LayoutGeometryTolerance.Score
            ? delta > 0
            : string.Compare(candidate.Candidate.Id, current.Candidate.Id, StringComparison.Ordinal) < 0;
    }

    private static bool SamePlacements(
        IReadOnlyList<LayoutTextPlacement> first,
        IReadOnlyList<LayoutTextPlacement> second)
    {
        if (first.Count != second.Count)
        {
            return false;
        }

        var byId = second.ToDictionary(item => item.ItemId, StringComparer.Ordinal);
        return first.All(item => byId.TryGetValue(item.ItemId, out var other) &&
            (item.Position - other.Position).Length <= LayoutGeometryTolerance.Distance &&
            Math.Abs(LayoutAngles.NormalizeSigned(item.RotationRadians - other.RotationRadians)) <= LayoutGeometryTolerance.Angle);
    }

    private static bool PreviousMatchesRequest(AnnotationLayoutRequest request)
    {
        var requested = request.Texts.Select(text => text.ItemId).OrderBy(value => value, StringComparer.Ordinal).ToArray();
        var previous = request.PreviousLayout!.Placements.Select(item => item.ItemId).OrderBy(value => value, StringComparer.Ordinal).ToArray();
        return requested.SequenceEqual(previous, StringComparer.Ordinal);
    }

    private SpatialIndex<LayoutObstacle> CreateObstacleIndex(IEnumerable<LayoutObstacle> obstacles)
    {
        var index = new SpatialIndex<LayoutObstacle>(
            _options.SpatialCellSize,
            _options.MaximumIndexedCellsPerItem,
            obstacle => obstacle.Footprint.Bounds);
        foreach (var obstacle in obstacles.OrderBy(item => item.Id, StringComparer.Ordinal))
        {
            index.Add(obstacle);
        }

        return index;
    }

    private static void AddDecisionObstacles(
        PreparedAnnotation state,
        LayoutDecision decision,
        SpatialIndex<LayoutObstacle> obstacleIndex)
    {
        foreach (var placement in decision.Placements)
        {
            obstacleIndex.Add(new LayoutObstacle(
                $"planned:{state.Request.AnnotationId}:{placement.ItemId}",
                LayoutObstacleKind.PlannedText,
                placement.Footprint.Rectangle,
                placement.ItemId,
                LayoutObstacleMeasurementSource.PlannedApproximateMetrics));
        }
    }

    private enum CandidateKind
    {
        Existing,
        Legacy,
        PhaseAPreferred,
        Adjustment,
    }

    private sealed record LayoutCandidate(
        string Id,
        CandidateKind Kind,
        int Tier,
        double TangentialOffset,
        double NormalOffset,
        double MovementDistance,
        double DistanceFromPreferred,
        bool SideChanged,
        IReadOnlyList<LayoutTextPlacement> Placements);

    private sealed record CandidateEvaluation(
        LayoutCandidate Candidate,
        LegacyValidationResult Validation,
        LayoutScore Score,
        AnnotationGroupInvariantResult GroupInvariants);

    private sealed record PreparedAnnotation(
        AnnotationLayoutRequest Request,
        NetworkAssociation Association,
        LayoutCandidate Legacy,
        LayoutCandidate? Existing,
        MovementBudget Budget);

    private sealed class NetworkSegmentIndex
    {
        private readonly SpatialIndex<NetworkSegment> _index;

        internal NetworkSegmentIndex(
            IEnumerable<LayoutNetworkPath> paths,
            double cellSize,
            int maximumIndexedCellsPerItem)
        {
            _index = new SpatialIndex<NetworkSegment>(cellSize, maximumIndexedCellsPerItem, segment => segment.Bounds);
            foreach (var path in paths.OrderBy(item => item.Id, StringComparer.Ordinal))
            {
                for (var index = 0; index < path.Vertices.Count - 1; index++)
                {
                    var start = path.Vertices[index];
                    var end = path.Vertices[index + 1];
                    if ((end - start).Length > LayoutGeometryTolerance.Distance)
                    {
                        _index.Add(new NetworkSegment(path.Id, path.SemanticId, path.Layer, index, start, end));
                    }
                }
            }
        }

        internal long QueryCount => _index.QueryCount;

        internal IReadOnlyList<NetworkSegment> Query(LayoutAabb bounds) => _index.Query(bounds);

        internal NetworkAssociation? Associate(AnnotationLayoutRequest request, AnnotationLayoutOptions options)
        {
            var search = new LayoutAabb(
                request.Anchor.X - options.AssociationDistance,
                request.Anchor.Y - options.AssociationDistance,
                request.Anchor.X + options.AssociationDistance,
                request.Anchor.Y + options.AssociationDistance);
            var evaluated = Query(search)
                .Select(segment => SegmentCandidate.Create(segment, request.Anchor))
                .Where(candidate => candidate.Distance <= options.AssociationDistance + LayoutGeometryTolerance.Distance)
                .OrderBy(candidate => candidate.Distance)
                .ThenBy(candidate => candidate.Segment.PathId, StringComparer.Ordinal)
                .ThenBy(candidate => candidate.Segment.Ordinal)
                .ToArray();
            var matched = evaluated.FirstOrDefault(candidate => Matches(request, candidate.Segment));
            if (matched is not null)
            {
                return matched.ToAssociation();
            }

            if (!string.Equals(request.NetworkSemanticId, "nao_identificada", StringComparison.Ordinal))
            {
                return null;
            }

            var inferred = evaluated.Where(candidate => candidate.Distance <= options.UnknownNetworkInferenceDistance).ToArray();
            if (inferred.Length == 0)
            {
                return null;
            }

            var semanticId = inferred[0].Segment.SemanticId;
            return inferred.Any(candidate => candidate.Segment.SemanticId != semanticId &&
                Math.Abs(candidate.Distance - inferred[0].Distance) <= options.UnknownNetworkInferenceDistance)
                ? null
                : inferred[0].ToAssociation();
        }

        private static bool Matches(AnnotationLayoutRequest request, NetworkSegment segment) =>
            string.Equals(request.NetworkSemanticId, segment.SemanticId, StringComparison.Ordinal) ||
            request.AllowedNetworkLayers.Any(layer =>
                string.Equals(layer.Trim(), segment.Layer.Trim(), StringComparison.OrdinalIgnoreCase));
    }

    private sealed record NetworkSegment(
        string PathId,
        string SemanticId,
        string Layer,
        int Ordinal,
        LayoutPoint2 Start,
        LayoutPoint2 End)
    {
        internal LayoutAabb Bounds => new(
            Math.Min(Start.X, End.X), Math.Min(Start.Y, End.Y),
            Math.Max(Start.X, End.X), Math.Max(Start.Y, End.Y));
    }

    private sealed record SegmentCandidate(
        NetworkSegment Segment,
        LayoutPoint2 ClosestPoint,
        LayoutVector2 Tangent,
        double Distance)
    {
        internal static SegmentCandidate Create(NetworkSegment segment, LayoutPoint2 point)
        {
            var vector = segment.End - segment.Start;
            var lengthSquared = LayoutVector2.Dot(vector, vector);
            var projection = lengthSquared <= LayoutGeometryTolerance.Distance
                ? 0
                : LayoutVector2.Dot(point - segment.Start, vector) / lengthSquared;
            projection = Math.Max(0, Math.Min(1, projection));
            var closest = segment.Start + (vector * projection);
            return new SegmentCandidate(segment, closest, vector.Normalized(), (point - closest).Length);
        }

        internal NetworkAssociation ToAssociation() =>
            new(Segment.PathId, Segment.SemanticId, Segment.Layer, ClosestPoint, Tangent, IsFallback: false);
    }

    private sealed record NetworkAssociation(
        string PathId,
        string SemanticId,
        string Layer,
        LayoutPoint2 ClosestPoint,
        LayoutVector2 Tangent,
        bool IsFallback)
    {
        internal static NetworkAssociation HorizontalFallback(LayoutPoint2 anchor) =>
            new(string.Empty, string.Empty, string.Empty, anchor, new LayoutVector2(1, 0), IsFallback: true);
    }

    private sealed class SpatialIndex<T>
        where T : class
    {
        private readonly double _cellSize;
        private readonly int _maximumIndexedCellsPerItem;
        private readonly Func<T, LayoutAabb> _bounds;
        private readonly Dictionary<(long X, long Y), List<Entry>> _cells = [];
        private readonly List<Entry> _global = [];
        private int _nextId;

        internal SpatialIndex(double cellSize, int maximumIndexedCellsPerItem, Func<T, LayoutAabb> bounds)
        {
            _cellSize = cellSize;
            _maximumIndexedCellsPerItem = maximumIndexedCellsPerItem;
            _bounds = bounds;
        }

        internal long QueryCount { get; private set; }

        internal void Add(T item)
        {
            var entry = new Entry(_nextId++, item);
            var bounds = _bounds(item);
            var minX = Cell(bounds.MinX);
            var maxX = Cell(bounds.MaxX);
            var minY = Cell(bounds.MinY);
            var maxY = Cell(bounds.MaxY);
            var count = ((maxX - minX) + 1L) * ((maxY - minY) + 1L);
            if (count > _maximumIndexedCellsPerItem)
            {
                _global.Add(entry);
                return;
            }

            for (var x = minX; x <= maxX; x++)
            {
                for (var y = minY; y <= maxY; y++)
                {
                    if (!_cells.TryGetValue((x, y), out var bucket))
                    {
                        bucket = [];
                        _cells.Add((x, y), bucket);
                    }

                    bucket.Add(entry);
                }
            }
        }

        internal IReadOnlyList<T> Query(LayoutAabb bounds)
        {
            QueryCount++;
            var seen = new HashSet<int>();
            var result = new List<T>();
            foreach (var entry in _global)
            {
                if (seen.Add(entry.Id) && _bounds(entry.Item).Intersects(bounds))
                {
                    result.Add(entry.Item);
                }
            }

            var minX = Cell(bounds.MinX);
            var maxX = Cell(bounds.MaxX);
            var minY = Cell(bounds.MinY);
            var maxY = Cell(bounds.MaxY);
            for (var x = minX; x <= maxX; x++)
            {
                for (var y = minY; y <= maxY; y++)
                {
                    if (!_cells.TryGetValue((x, y), out var bucket))
                    {
                        continue;
                    }

                    foreach (var entry in bucket)
                    {
                        if (seen.Add(entry.Id) && _bounds(entry.Item).Intersects(bounds))
                        {
                            result.Add(entry.Item);
                        }
                    }
                }
            }

            return result;
        }

        private long Cell(double coordinate) => checked((long)Math.Floor(coordinate / _cellSize));

        private sealed record Entry(int Id, T Item);
    }
}
