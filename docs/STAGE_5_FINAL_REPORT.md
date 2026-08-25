# Fechamento da Etapa 5 — motor geométrico CAD

Data: 2026-08-10  
Estado: **concluída**

O adaptador CAD 2026 resolve as intenções `FollowNetwork` contra a geometria real do DWG e aplica o `DrawingPlan` sem AutoLISP, SCR ou arquivos intermediários.

Entregue:

- indexação e associação de `Line`, `Polyline`, `Polyline2d`, `Polyline3d`, `Arc` e `Spline`;
- ponto mais próximo, tangente, rotação legível e offsets;
- proteção contra falhas isoladas de entidades/layers do desenho;
- aplicação transacional, rollback, Undo e uma regeneração controlada;
- identidade XData determinística e atualização idempotente na reexecução;
- preservação de layers preexistentes do usuário.

Os smokes geométricos e de tipos de curva passaram no AutoCAD/Civil 3D 2026. Primeira execução: 7 entidades; segunda execução: 0 novas e 7 identidades reaproveitadas.

