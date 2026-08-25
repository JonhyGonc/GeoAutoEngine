;;; GeoAutoEngine - arquivo gerado automaticamente
;;; Carregue com APPLOAD. A rotina executa ao carregar.
;;; Para executar novamente, digite GEOAUTOPROF.
;;; GEOAUTOENGINE permanece como comando de compatibilidade.

(vl-load-com)

(setq *geoauto-altura-texto* 0.5)
(setq *geoauto-afastamento-texto* 0.1)
(setq *geoauto-raio-primario* 0.10)
(setq *geoauto-raio-secundario* 0.50)
(setq *geoauto-raio-maximo* 0.70)
(setq *geoauto-raio-estendido-mesma-rede* 5.00)
(setq *geoauto-raio-ni-consolidada* 0.10)
(setq *geoauto-tolerancia-ambiguidade* 0.10)
(setq *geoauto-amostra-tangente* 0.05)
(setq *geoauto-tolerancia-tangente-xy* 1e-12)
(setq *geoauto-tolerancia-parametro-vertice* 1e-08)
(setq *geoauto-tolerancia-espacial-vertice* 0.0001)
(setq *geoauto-tolerancia-validacao-rotacao* 1e-08)
(setq *geoauto-permitir-outra-rede* T)
(setq *geoauto-estilo-texto* "GEO_ARIAL")
(setq *geoauto-estilo-anotacao-divisa* "GEO_ARIAL_BOLD")
(setq *geoauto-xdata-app* "GEOAUTOENGINE_V2_F09ECA895231")
(setq *geoauto-execution-id* "f09eca8952314eb1a04c8044909b16a3")
(setq *geoauto-raio-atencao* 0.75)
(setq *geoauto-cor-atencao* 1)
(setq *geoauto-layer-anotacao-divisa* "-Texto")
(setq *geoauto-cor-anotacao-divisa* 7)
(setq *geoauto-tolerancia-sobreposicao-blocos* 1)
(setq *geoauto-raio-protecao-texto-pv* 1.5)
(setq *geoauto-debug* T)
(setq *geoauto-habilitar-rotacao* T)
(setq *geoauto-habilitar-colisao-textos* T)
(setq *geoauto-habilitar-blocos* T)
(setq *geoauto-considerar-textos-existentes-na-colisao* nil)
(setq *geoauto-intervalo-progresso* 100)
(setq *geoauto-fase-atual* "CARREGAMENTO")
(setq *geoauto-passo-deslocamento-longitudinal* 0.5)
(setq *geoauto-max-deslocamento-longitudinal* 2)
(setq *geoauto-passo-afastamento-perpendicular* 0.1)
(setq *geoauto-max-afastamento-perpendicular-adicional* 0.3)
(setq *geoauto-fator-passo-longitudinal* *geoauto-passo-deslocamento-longitudinal*)
(setq *geoauto-max-tentativas-deslocamento* (fix (/ *geoauto-max-deslocamento-longitudinal* *geoauto-passo-deslocamento-longitudinal*)))
(setq *geoauto-fator-margem-colisao* 0.3)
(setq *geoauto-verificar-textos-existentes* *geoauto-considerar-textos-existentes-na-colisao*)
(setq *geoauto-fator-passo-perpendicular* *geoauto-passo-afastamento-perpendicular*)
(setq *geoauto-max-tentativas-perpendiculares* (fix (/ *geoauto-max-afastamento-perpendicular-adicional* *geoauto-passo-afastamento-perpendicular*)))
(setq *geoauto-fator-margem-polyline-texto* 0.2)
(setq *geoauto-verificar-interferencia-polylines* T)
(setq *geoauto-registro-id-atual* nil)
(setq *geoauto-codigo-atual* nil)
(setq *geoauto-ponto-original-atual* nil)
(setq *geoauto-text-occupied-boxes* nil)
(setq *geoauto-curve-bboxes* nil)
(setq *geoauto-pontos-circulos-atencao* nil)
(setq *geoauto-limite-amostra-agua* 10)

(setq *geoauto-layers-texto*
  '(
    "GF-Rede Água"
    "GF-Rede Esgoto"
    "GF-Rede Pluvial"
    "GF-Rede Telefone"
    "GF-Rede Fibra Optica"
    "GF-Rede Eletrica"
    "GF-Rede Sem Descrição de Uso"
    "GF-Diagnostico"
    "GF-Rede Esgoto Industrial"
    "GF-Rede de Incendio"
    "GF-Rede Gás"
  )
)

(setq *geoauto-layers-agua*
  '(
    "GF-Rede Água"
    "REDE_AGUA"
    "AGUA"
    "AF"
    "AFT"
  )
)

(setq *geoauto-layers-tubulacoes*
  '(
    "GF-Rede Água"
    "REDE_AGUA"
    "AGUA"
    "AF"
    "AFT"
    "GF-Rede Pluvial"
    "REDE_DRENAGEM"
    "DRENAGEM"
    "PLUVIAL"
    "GF-Rede Esgoto"
    "REDE_ESGOTO"
    "ESGOTO"
    "GF-Rede Esgoto Industrial"
    "REDE_ESGOTO_INDUSTRIAL"
    "ESGOTO INDUSTRIAL"
    "GF-Rede Eletrica"
    "REDE_ELETRICA"
    "ELETRICA"
    "GF-Rede Telefone"
    "REDE_TELEFONE"
    "TELEFONE"
    "GF-Rede Fibra Optica"
    "REDE_FIBRA"
    "FIBRA"
    "FIBRA OPTICA"
    "GF-Rede de Incendio"
    "REDE_INCENDIO"
    "INCENDIO"
    "GF-Rede Gás"
    "REDE_GAS"
    "GAS"
    "GF-Rede Sem Descrição de Uso"
    "REDE_NAO_IDENTIFICADA"
    "REDE_OUTRA"
  )
)

(setq *geoauto-layers-estruturas-pluviais*
  '(
  )
)

(setq *geoauto-layers-consolidados*
  '(
    "GF-Rede Água"
    "REDE_AGUA"
    "AGUA"
    "AF"
    "AFT"
    "GF-Rede Pluvial"
    "REDE_DRENAGEM"
    "DRENAGEM"
    "PLUVIAL"
    "GF-Rede Esgoto"
    "REDE_ESGOTO"
    "ESGOTO"
    "GF-Rede Esgoto Industrial"
    "REDE_ESGOTO_INDUSTRIAL"
    "ESGOTO INDUSTRIAL"
    "GF-Rede Eletrica"
    "REDE_ELETRICA"
    "ELETRICA"
    "GF-Rede Telefone"
    "REDE_TELEFONE"
    "TELEFONE"
    "GF-Rede Fibra Optica"
    "REDE_FIBRA"
    "FIBRA"
    "FIBRA OPTICA"
    "GF-Rede de Incendio"
    "REDE_INCENDIO"
    "INCENDIO"
    "GF-Rede Gás"
    "REDE_GAS"
    "GAS"
  )
)

(setq *geoauto-layers-texto-por-curva-consolidada*
  '(
    ("GF-REDE AGUA" . "GF-Rede Água")
    ("REDE_AGUA" . "GF-Rede Água")
    ("AGUA" . "GF-Rede Água")
    ("AF" . "GF-Rede Água")
    ("AFT" . "GF-Rede Água")
    ("GF-REDE PLUVIAL" . "GF-Rede Pluvial")
    ("REDE_DRENAGEM" . "GF-Rede Pluvial")
    ("DRENAGEM" . "GF-Rede Pluvial")
    ("PLUVIAL" . "GF-Rede Pluvial")
    ("GF-REDE ESGOTO" . "GF-Rede Esgoto")
    ("REDE_ESGOTO" . "GF-Rede Esgoto")
    ("ESGOTO" . "GF-Rede Esgoto")
    ("GF-REDE ESGOTO INDUSTRIAL" . "GF-Rede Esgoto Industrial")
    ("REDE_ESGOTO_INDUSTRIAL" . "GF-Rede Esgoto Industrial")
    ("ESGOTO INDUSTRIAL" . "GF-Rede Esgoto Industrial")
    ("GF-REDE ELETRICA" . "GF-Rede Eletrica")
    ("REDE_ELETRICA" . "GF-Rede Eletrica")
    ("ELETRICA" . "GF-Rede Eletrica")
    ("GF-REDE TELEFONE" . "GF-Rede Telefone")
    ("REDE_TELEFONE" . "GF-Rede Telefone")
    ("TELEFONE" . "GF-Rede Telefone")
    ("GF-REDE FIBRA OPTICA" . "GF-Rede Fibra Optica")
    ("REDE_FIBRA" . "GF-Rede Fibra Optica")
    ("FIBRA" . "GF-Rede Fibra Optica")
    ("FIBRA OPTICA" . "GF-Rede Fibra Optica")
    ("GF-REDE DE INCENDIO" . "GF-Rede de Incendio")
    ("REDE_INCENDIO" . "GF-Rede de Incendio")
    ("INCENDIO" . "GF-Rede de Incendio")
    ("GF-REDE GAS" . "GF-Rede Gás")
    ("REDE_GAS" . "GF-Rede Gás")
    ("GAS" . "GF-Rede Gás")
  )
)

(setq *geoauto-rede-nao-identificada* "Não identificada")

(setq *geoauto-registros*
  '(
    ("GF-Rede Água" (10 0.2 0.0) "P.1.00" "∅100" "Água potável" nil "REG-000001" "AF1.00D100" ("GF-Rede Água" "REDE_AGUA" "AGUA" "AF" "AFT"))
    ("GF-Rede Esgoto" (20.2 10 0.0) "P.1.20" "∅200" "Esgoto sanitário" nil "REG-000002" "ES1.20D200" ("GF-Rede Esgoto" "REDE_ESGOTO" "ESGOTO"))
    ("GF-Rede Pluvial" (40 20.2 0.0) "P.0.80" "∅300" "Águas pluviais" T "REG-000003" "AP0.80D300" ("GF-Rede Pluvial" "REDE_DRENAGEM" "DRENAGEM" "PLUVIAL"))
    ("GF-Rede Telefone" (30 30.2 0.0) "P.0.60" "∅50" "Telefone" nil "REG-000004" "TL0.60D50" ("GF-Rede Telefone" "REDE_TELEFONE" "TELEFONE"))
    ("GF-Rede Fibra Optica" (50 40.2 0.0) "P.0.50" "∅50" "Fibra óptica" nil "REG-000005" "FB0.50D50" ("GF-Rede Fibra Optica" "REDE_FIBRA" "FIBRA" "FIBRA OPTICA"))
    ("GF-Rede Eletrica" (70 50.2 0.0) "P.0.70" "∅100" "Rede elétrica" nil "REG-000006" "EL0.70D100" ("GF-Rede Eletrica" "REDE_ELETRICA" "ELETRICA"))
    ("GF-Rede Sem Descrição de Uso" (80 60.2 0.0) "P.0.90" "∅80" "Não identificada" nil "REG-000007" "NI0.90D80" ("GF-Rede Sem Descrição de Uso" "REDE_NAO_IDENTIFICADA"))
  )
)

(setq *geoauto-anotacoes-divisa*
  '(
  )
)

(setq *geoauto-alertas-codigos-desconhecidos*
  '(
    ("GF-Diagnostico" (80 80 0) "CODIGO_DESCONHECIDO_X" "DIAGNOSTICO-000001")
  )
)

(setq *geoauto-estruturas-pluviais*
  '(
  )
)

(setq *geoauto-alertas-estruturas-pluviais*
  '(
  )
)

(setq *geoauto-instrucoes-blocos*
  '(
    ("REGISTRO_AGUA_POTAVEL" "rg_agua_potavel" "C:/Users/João/Downloads/GeoAutoEngine/assets/blocks/rg_agua_potavel.dwg" "GF-Rede Água" (85 0.1 0) 1 1 1 "follow_network" 0 "REGISTRO_AF" "RG PROF 0.80" "Água potável" "insertion_point" "BLOCO-000001" ("GF-Rede Água" "REDE_AGUA" "AGUA" "AF" "AFT"))
    ("PV_ESGOTO_SANITARIO" "pv_esgoto_sanitario" "C:/Users/João/Downloads/GeoAutoEngine/assets/blocks/pv_esgoto_sanitario.dwg" "GF-Rede Esgoto" (20 75 0) 1 1 1 "fixed" 0 "PV_ES" "PVES 1.20D150" "Esgoto sanitário" "insertion_point" "BLOCO-000002" ("GF-Rede Esgoto" "REDE_ESGOTO" "ESGOTO"))
  )
)

(setq *geoauto-blocos-arquivos-ausentes*
  '(
  )
)

(setq *geoauto-blocos-sem-mapeamento*
  '(
  )
)

(setq *geoauto-blocos-coordenadas-invalidas*
  '(
  )
)

(setq *geoauto-blocos-duplicatas-ignoradas*
  '(
  )
)

(setq *geoauto-blocos-proximidade-ignorados*
  '(
  )
)

(defun geo:ensure-layer (doc name / layers layer)
  (setq layers (vla-get-Layers doc))
  (if (tblsearch "LAYER" name)
    (setq layer (vla-Item layers name))
    (setq layer (vla-Add layers name))
  )
  (vla-put-LayerOn layer :vlax-true)
  (if (= :vlax-true (vla-get-Freeze layer))
    (vla-put-Freeze layer :vlax-false)
  )
  (if (= :vlax-true (vla-get-Lock layer))
    (vla-put-Lock layer :vlax-false)
  )
  layer
)

(defun geo:ensure-text-style
  (doc name font-name font-file bold italic
   / styles style font-result)
  (setq styles (vla-get-TextStyles doc))
  (if (tblsearch "STYLE" name)
    (setq style (vla-Item styles name))
    (setq style (vla-Add styles name))
  )
  (vla-put-Height style 0.0)
  (setq font-result
    (vl-catch-all-apply
      'vla-SetFont
      (list style font-name bold italic 0 34)
    )
  )
  (if (and (vl-catch-all-error-p font-result)
           (vlax-property-available-p style 'FontFile T))
    (vla-put-FontFile style font-file)
  )
  style
)

(defun geoauto-chamada-segura (funcao argumentos / resultado)
  (setq resultado (vl-catch-all-apply funcao argumentos))
  (if (vl-catch-all-error-p resultado) nil resultado)
)

(defun geoauto-debug (mensagem)
  (if *geoauto-debug*
    (princ (strcat "\n[GeoAutoEngine] " mensagem))
  )
  (princ)
)

; Wrappers pequenos preservam chamadas legíveis no LSP e permitem que o
; comando principal as execute com vl-catch-all-apply.
(defun geoauto-iniciar-undo (doc)
  (vla-StartUndoMark doc)
)

(defun geoauto-finalizar-undo (doc)
  (vla-EndUndoMark doc)
)

(defun geoauto-validar-layer (doc layer)
  (geo:ensure-layer doc layer)
)

(defun geoauto-configurar-layer-cor
  (doc layer cor / objeto resultado)
  (setq objeto (geo:ensure-layer doc layer))
  (setq resultado
    (vl-catch-all-apply 'vla-put-Color (list objeto cor))
  )
  (if (vl-catch-all-error-p resultado) nil objeto)
)

(defun geoauto-validar-estilo
  (doc nome fonte arquivo-fonte negrito italico)
  (geo:ensure-text-style
    doc nome fonte arquivo-fonte negrito italico
  )
)

(defun geoauto-coletar-curvas-seguro (space)
  (geoauto-coletar-curvas space)
)

(defun geoauto-coletar-textos-existentes-seguro
  (space layers-validos margem)
  (geoauto-coletar-bboxes-textos-existentes
    space layers-validos margem
  )
)

(defun geoauto-vetor-normalizado (vetor / x y z modulo)
  (if (and (listp vetor)
           (numberp (car vetor))
           (numberp (cadr vetor)))
    (progn
      (setq x (float (car vetor)))
      (setq y (float (cadr vetor)))
      (setq z
        (if (numberp (caddr vetor)) (float (caddr vetor)) 0.0)
      )
      (setq modulo (sqrt (+ (* x x) (* y y) (* z z))))
      (if (> modulo 1.0e-12)
        (list (/ x modulo) (/ y modulo) (/ z modulo))
        nil
      )
    )
    nil
  )
)

(defun geoauto-ponto-deslocado
  (ponto tangente distancia / vetor perpendicular z)
  (setq vetor (geoauto-vetor-normalizado tangente))
  (if vetor
    (progn
      (setq perpendicular
        (list (- (cadr vetor)) (car vetor) 0.0)
      )
      (setq z (if (numberp (caddr ponto)) (caddr ponto) 0.0))
      (list
        (+ (car ponto) (* (car perpendicular) distancia))
        (+ (cadr ponto) (* (cadr perpendicular) distancia))
        z
      )
    )
    nil
  )
)

(defun geoauto-ponto-deslocado-longitudinal
  (ponto tangente distancia / vetor z)
  (setq vetor (geoauto-vetor-normalizado tangente))
  (if vetor
    (progn
      (setq z (if (numberp (caddr ponto)) (caddr ponto) 0.0))
      (list
        (+ (car ponto) (* (car vetor) distancia))
        (+ (cadr ponto) (* (cadr vetor) distancia))
        z
      )
    )
    ponto
  )
)

(defun geoauto-ponto-deslocado-vetor
  (ponto vetor distancia / z)
  ; Usa diretamente o vetor canônico armazenado no contexto. Não normaliza,
  ; não inverte e não recalcula orientação durante o posicionamento.
  (if (and ponto vetor
           (numberp (car vetor))
           (numberp (cadr vetor)))
    (progn
      (setq z (if (numberp (caddr ponto)) (caddr ponto) 0.0))
      (list
        (+ (car ponto) (* (car vetor) distancia))
        (+ (cadr ponto) (* (cadr vetor) distancia))
        z
      )
    )
    ponto
  )
)

(defun geoauto-distancia-2d (ponto-a ponto-b / dx dy)
  (setq dx (- (car ponto-a) (car ponto-b)))
  (setq dy (- (cadr ponto-a) (cadr ponto-b)))
  (sqrt (+ (* dx dx) (* dy dy)))
)

(defun geoauto-colapsar-espacos (texto)
  (setq texto
    (vl-string-translate
      (strcat (chr 9) (chr 10) (chr 13))
      "   "
      texto
    )
  )
  (while (vl-string-search "  " texto)
    (setq texto (vl-string-subst " " "  " texto))
  )
  (vl-string-trim " " texto)
)

(defun geoauto-normalizar-layer (layer / texto)
  (setq texto (strcase (if layer layer "")))
  (setq texto
    (vl-string-translate
      "ÁÀÂÃÄÉÈÊËÍÌÎÏÓÒÔÕÖÚÙÛÜÇ"
      "AAAAAEEEEIIIIOOOOOUUUUC"
      texto
    )
  )
  (geoauto-colapsar-espacos texto)
)

(defun geoauto-layer-texto-por-curva-consolidada
  (layer / chave item)
  (setq chave
    (geoauto-normalizar-layer (geoauto-sufixo-layer-xref layer))
  )
  (setq item
    (assoc chave *geoauto-layers-texto-por-curva-consolidada*)
  )
  (if item (cdr item) nil)
)

(defun geoauto-ultimo-separador-layer (layer / inicio encontrado ultimo)
  (setq inicio 0)
  (setq ultimo nil)
  (while (setq encontrado (vl-string-search "|" layer inicio))
    (setq ultimo encontrado)
    (setq inicio (1+ encontrado))
  )
  ultimo
)

(defun geoauto-sufixo-layer-xref (layer / posicao)
  (setq layer (if layer layer ""))
  (setq posicao (geoauto-ultimo-separador-layer layer))
  (if (numberp posicao)
    (vl-string-trim " " (substr layer (+ posicao 2)))
    layer
  )
)

(defun geoauto-chave-layer-em-lista-p (chave layers-validos)
  (if layers-validos
    (if (= chave (geoauto-normalizar-layer (car layers-validos)))
      T
      (geoauto-chave-layer-em-lista-p chave (cdr layers-validos))
    )
    nil
  )
)

(defun geoauto-modo-correspondencia-layer
  (layer layers-validos / chave sufixo-normalizado)
  (setq chave (geoauto-normalizar-layer layer))
  (cond
    ((geoauto-chave-layer-em-lista-p chave layers-validos) 'EXATO)
    ((and layer (vl-string-search "|" layer))
      (setq sufixo-normalizado
        (geoauto-normalizar-layer (geoauto-sufixo-layer-xref layer))
      )
      (if (geoauto-chave-layer-em-lista-p sufixo-normalizado layers-validos)
        'SUFIXO_XREF
        nil
      )
    )
    (T nil)
  )
)

(defun geoauto-layer-valido-p (layer layers-validos)
  (if (geoauto-modo-correspondencia-layer layer layers-validos) T nil)
)

(defun geoauto-tipo-curva-valido-p (entidade / nome-objeto)
  (setq nome-objeto
    (geoauto-chamada-segura 'vla-get-ObjectName (list entidade))
  )
  (if (member nome-objeto '("AcDbLine" "AcDbPolyline" "AcDb2dPolyline" "AcDb3dPolyline" "AcDbArc" "AcDbSpline")) T nil)
)

(defun geoauto-tipo-potencial-curva-nao-suportada-p (nome-objeto / chave)
  (setq chave (strcase (if nome-objeto nome-objeto "")))
  (if
    (or
      (member nome-objeto '("AcDbArc" "AcDbCircle" "AcDbEllipse" "AcDbSpline" "AcDbHelix"))
      (vl-string-search "FEATURELINE" chave)
      (vl-string-search "ALIGNMENT" chave)
      (vl-string-search "SPLINE" chave)
      (vl-string-search "ARC" chave)
    )
    T
    nil
  )
)

(defun geoauto-curve-closed-p (entidade / resultado)
  (setq resultado
    (vl-catch-all-apply 'vlax-curve-isClosed (list entidade))
  )
  (if (vl-catch-all-error-p resultado)
    nil
    (if resultado T nil)
  )
)

(defun geoauto-entidade-valida-p (entidade)
  (if (and (geoauto-tipo-curva-valido-p entidade)
           (not (geoauto-curve-closed-p entidade)))
    T
    nil
  )
)

(defun geoauto-adicionar-string-unica (valor lista-valores)
  (if (member valor lista-valores)
    lista-valores
    (cons valor lista-valores)
  )
)

(defun geoauto-indexar-curva-por-layer
  (layer entidade indice / chave item)
  (setq chave (geoauto-normalizar-layer layer))
  (if (= chave "")
    indice
    (progn
      (setq item (assoc chave indice))
      (if item
        (subst
          (cons chave (cons entidade (cdr item)))
          item
          indice
        )
        (cons (list chave entidade) indice)
      )
    )
  )
)

(defun geoauto-tabela-layer-tipo-incrementar
  (layer tipo tabela / primeiro)
  (if (null tabela)
    (list (list layer tipo 1))
    (progn
      (setq primeiro (car tabela))
      (if (and
            (= (geoauto-normalizar-layer layer)
               (geoauto-normalizar-layer (nth 0 primeiro)))
            (= (strcase tipo) (strcase (nth 1 primeiro))))
        (cons
          (list (nth 0 primeiro) (nth 1 primeiro) (1+ (nth 2 primeiro)))
          (cdr tabela)
        )
        (cons
          primeiro
          (geoauto-tabela-layer-tipo-incrementar layer tipo (cdr tabela))
        )
      )
    )
  )
)

(defun geoauto-block-reference-p (nome-objeto)
  (if (member nome-objeto '("AcDbBlockReference" "AcDbMInsertBlock")) T nil)
)

(defun geoauto-referencia-xref-p
  (entidade / doc blocos nome definicao valor)
  (setq doc (geoauto-chamada-segura 'vla-get-Document (list entidade)))
  (setq nome (geoauto-chamada-segura 'vla-get-Name (list entidade)))
  (if (and doc nome)
    (progn
      (setq blocos (geoauto-chamada-segura 'vla-get-Blocks (list doc)))
      (if blocos
        (setq definicao
          (geoauto-chamada-segura 'vla-Item (list blocos nome))
        )
      )
      (if definicao
        (setq valor
          (geoauto-chamada-segura 'vla-get-IsXRef (list definicao))
        )
      )
    )
  )
  (if (or (= valor :vlax-true) (= valor -1)) T nil)
)

(defun geoauto-variant-para-lista
  (valor / bruto resultado)
  (setq bruto
    (vl-catch-all-apply 'vlax-variant-value (list valor))
  )
  (if (vl-catch-all-error-p bruto)
    (setq bruto valor)
  )
  (if (listp bruto)
    bruto
    (progn
      (setq resultado
        (vl-catch-all-apply 'vlax-safearray->list (list bruto))
      )
      (if (vl-catch-all-error-p resultado) nil resultado)
    )
  )
)

(defun geoauto-obter-ponto-insercao-referencia
  (entidade / valor ponto)
  (setq valor
    (vl-catch-all-apply 'vla-get-InsertionPoint (list entidade))
  )
  (if (vl-catch-all-error-p valor)
    nil
    (progn
      (setq ponto (geoauto-variant-para-lista valor))
      (if (and
            (listp ponto)
            (numberp (car ponto))
            (numberp (cadr ponto)))
        (list
          (float (car ponto))
          (float (cadr ponto))
          (if (numberp (caddr ponto)) (float (caddr ponto)) 0.0)
        )
        nil
      )
    )
  )
)

(defun geoauto-criar-referencia-diagnostico
  (entidade / bbox tipo nome layer handle ponto-insercao)
  (setq bbox (geoauto-get-entity-bbox entidade))
  (setq tipo (if (geoauto-referencia-xref-p entidade) 'XREF 'BLOCO))
  (setq nome (geoauto-chamada-segura 'vla-get-Name (list entidade)))
  (setq layer (geoauto-chamada-segura 'vla-get-Layer (list entidade)))
  (setq handle (geoauto-handle-entidade entidade))
  (setq ponto-insercao
    (geoauto-obter-ponto-insercao-referencia entidade)
  )
  (list entidade bbox tipo nome layer handle ponto-insercao)
)

(defun geoauto-coletar-pontos-blocos-existentes
  (referencias / pontos referencia ponto nome familia)
  (setq pontos nil)
  (foreach referencia referencias
    (setq ponto (nth 6 referencia))
    (setq nome (nth 3 referencia))
    (setq familia (geoauto-chave-familia-bloco nome))
    (if (and (eq (nth 2 referencia) 'BLOCO) ponto)
      (setq pontos (cons (list familia ponto) pontos))
    )
  )
  pontos
)

(defun geoauto-chave-familia-bloco (nome / chave)
  (setq chave (strcase (if nome nome "")))
  (cond
    ((and (>= (strlen chave) 2) (= (substr chave 1 2) "PV")) "PV")
    ((vl-string-search "POSTE" chave) "POSTE")
    (T chave)
  )
)

(defun geoauto-ponto-ocupado-por-bloco-p
  (nome-bloco ponto pontos tolerancia
   / ocupado ocupacao familia ponto-existente)
  (setq ocupado nil)
  (setq familia (geoauto-chave-familia-bloco nome-bloco))
  (foreach ocupacao pontos
    (setq ponto-existente (nth 1 ocupacao))
    (if (and
          (null ocupado)
          (= familia (nth 0 ocupacao))
          (<
            (geoauto-distancia-2d ponto ponto-existente)
            tolerancia))
      (setq ocupado T)
    )
  )
  ocupado
)

(defun geoauto-coletar-curvas
  (espaco
   / fechadas-ignoradas layers-abertos layers-fechados
     layers-curvas-desenho curvas-abertas quantidade-abertas
     curvas-por-layer entidade layer nome-objeto
     tabela-abertas tabela-fechadas tabela-nao-suportadas
     curvas-fechadas-por-layer entidades-nao-suportadas-por-layer
     referencias-diagnostico referencia quantidade-blocos quantidade-xrefs)
  (setq fechadas-ignoradas 0)
  (setq layers-abertos nil)
  (setq layers-fechados nil)
  (setq layers-curvas-desenho nil)
  (setq curvas-abertas nil)
  (setq curvas-por-layer nil)
  (setq quantidade-abertas 0)
  (setq tabela-abertas nil)
  (setq tabela-fechadas nil)
  (setq tabela-nao-suportadas nil)
  (setq curvas-fechadas-por-layer nil)
  (setq entidades-nao-suportadas-por-layer nil)
  (setq referencias-diagnostico nil)
  (setq quantidade-blocos 0)
  (setq quantidade-xrefs 0)
  (vlax-for entidade espaco
    (setq nome-objeto
      (geoauto-chamada-segura 'vla-get-ObjectName (list entidade))
    )
    (setq layer
      (geoauto-chamada-segura 'vla-get-Layer (list entidade))
    )
    (if (geoauto-tipo-curva-valido-p entidade)
      (progn
        (if layer
          (setq layers-curvas-desenho
            (geoauto-adicionar-string-unica layer layers-curvas-desenho)
          )
        )
        (if (geoauto-curve-closed-p entidade)
          (progn
            (setq fechadas-ignoradas (1+ fechadas-ignoradas))
            (if layer
              (progn
                (setq layers-fechados
                  (geoauto-adicionar-string-unica layer layers-fechados)
                )
                (setq curvas-fechadas-por-layer
                  (geoauto-indexar-curva-por-layer
                    layer entidade curvas-fechadas-por-layer
                  )
                )
                (setq tabela-fechadas
                  (geoauto-tabela-layer-tipo-incrementar
                    layer nome-objeto tabela-fechadas
                  )
                )
              )
            )
          )
          (if (geoauto-entidade-valida-p entidade)
            (progn
              (if layer
                (progn
                  (setq layers-abertos
                    (geoauto-adicionar-string-unica layer layers-abertos)
                  )
                  (setq curvas-por-layer
                    (geoauto-indexar-curva-por-layer
                      layer entidade curvas-por-layer
                    )
                  )
                  (setq tabela-abertas
                    (geoauto-tabela-layer-tipo-incrementar
                      layer nome-objeto tabela-abertas
                    )
                  )
                )
              )
              (setq curvas-abertas (cons entidade curvas-abertas))
              (setq quantidade-abertas (1+ quantidade-abertas))
            )
          )
        )
      )
      (progn
        (if (and layer nome-objeto)
          (setq tabela-nao-suportadas
            (geoauto-tabela-layer-tipo-incrementar
              layer nome-objeto tabela-nao-suportadas
            )
          )
        )
        (if (geoauto-block-reference-p nome-objeto)
          (progn
            (setq referencia
              (geoauto-criar-referencia-diagnostico entidade)
            )
            (setq referencias-diagnostico
              (cons referencia referencias-diagnostico)
            )
            (if (eq (nth 2 referencia) 'XREF)
              (setq quantidade-xrefs (1+ quantidade-xrefs))
              (setq quantidade-blocos (1+ quantidade-blocos))
            )
          )
          (if (and layer
                   (geoauto-tipo-potencial-curva-nao-suportada-p nome-objeto))
            (setq entidades-nao-suportadas-por-layer
              (geoauto-indexar-curva-por-layer
                layer entidade entidades-nao-suportadas-por-layer
              )
            )
          )
        )
      )
    )
  )
  (setq curvas-abertas (reverse curvas-abertas))
  (list
    curvas-abertas
    fechadas-ignoradas
    (reverse layers-abertos)
    (reverse layers-fechados)
    quantidade-abertas
    (reverse layers-curvas-desenho)
    curvas-abertas
    curvas-por-layer
    tabela-abertas
    tabela-fechadas
    tabela-nao-suportadas
    (reverse referencias-diagnostico)
    entidades-nao-suportadas-por-layer
    curvas-fechadas-por-layer
    quantidade-blocos
    quantidade-xrefs
  )
)

(defun geoauto-adicionar-entidades-unicas (entidades resultado / entidade)
  (foreach entidade entidades
    (if (not (member entidade resultado))
      (setq resultado (cons entidade resultado))
    )
  )
  resultado
)

(defun geoauto-obter-curvas-por-layers
  (curvas-por-layer layers-validos / resultado layer chave item sufixo)
  (setq resultado nil)
  ; Primeiro aplica correspondência exata normalizada.
  (foreach layer layers-validos
    (setq chave (geoauto-normalizar-layer layer))
    (setq item (assoc chave curvas-por-layer))
    (if item
      (setq resultado
        (geoauto-adicionar-entidades-unicas (cdr item) resultado)
      )
    )
  )
  ; Depois aceita apenas o sufixo exato de layers prefixados por XREF.
  (foreach item curvas-por-layer
    (setq chave (car item))
    (if (vl-string-search "|" chave)
      (progn
        (setq sufixo
          (geoauto-normalizar-layer (geoauto-sufixo-layer-xref chave))
        )
        (if (geoauto-chave-layer-em-lista-p sufixo layers-validos)
          (setq resultado
            (geoauto-adicionar-entidades-unicas (cdr item) resultado)
          )
        )
      )
    )
  )
  (reverse resultado)
)

(defun geoauto-handle-entidade (entidade / handle)
  (setq handle
    (geoauto-chamada-segura 'vla-get-Handle (list entidade))
  )
  (if handle (strcase handle) "")
)


(defun geoauto-ponto-segmento-mais-proximo-2d
  (ponto a b / ax ay bx by px py dx dy comprimento2 tproj qx qy)
  (setq ax (car a))
  (setq ay (cadr a))
  (setq bx (car b))
  (setq by (cadr b))
  (setq px (car ponto))
  (setq py (cadr ponto))
  (setq dx (- bx ax))
  (setq dy (- by ay))
  (setq comprimento2 (+ (* dx dx) (* dy dy)))
  (if (> comprimento2 1.0e-18)
    (progn
      (setq tproj
        (/ (+ (* (- px ax) dx) (* (- py ay) dy)) comprimento2)
      )
      (setq tproj (max 0.0 (min 1.0 tproj)))
      (setq qx (+ ax (* tproj dx)))
      (setq qy (+ ay (* tproj dy)))
      (list qx qy 0.0)
    )
    (list ax ay 0.0)
  )
)

(defun geoauto-obter-todas-curvas-indexadas
  (curvas-por-layer / resultado item)
  ; A associação geométrica não usa layer como barreira eliminatória.
  ; A tabela já contém somente curvas abertas validadas e é coletada uma vez.
  (setq resultado nil)
  (foreach item curvas-por-layer
    (setq resultado
      (geoauto-adicionar-entidades-unicas (cdr item) resultado)
    )
  )
  (reverse resultado)
)

(defun geoauto-candidato-menor-p (candidato-a candidato-b / da db ha hb)
  (setq da (nth 2 candidato-a))
  (setq db (nth 2 candidato-b))
  (setq ha (nth 4 candidato-a))
  (setq hb (nth 4 candidato-b))
  (if (equal da db 1.0e-12)
    (vl-string-lessp ha hb)
    (< da db)
  )
)

(defun geoauto-classificar-faixa
  (distancia raio-primario raio-secundario raio-maximo)
  (cond
    ((<= distancia raio-primario) 'PRIMARIO)
    ((<= distancia raio-secundario) 'SECUNDARIO)
    ((<= distancia raio-maximo) 'MAXIMO)
    (T nil)
  )
)

(defun geoauto-raio-da-faixa
  (faixa raio-primario raio-secundario raio-maximo)
  (cond
    ((eq faixa 'PRIMARIO) raio-primario)
    ((eq faixa 'SECUNDARIO) raio-secundario)
    ((eq faixa 'MAXIMO) raio-maximo)
    (T nil)
  )
)

(defun geoauto-filtrar-candidatos-no-raio
  (candidatos raio / resultado candidato)
  (setq resultado nil)
  (foreach candidato candidatos
    (if (<= (nth 2 candidato) raio)
      (setq resultado (cons candidato resultado))
    )
  )
  (reverse resultado)
)

(defun geoauto-filtrar-candidatos-rede-principal
  (candidatos / resultado candidato)
  ; O índice 6 guarda o modo de correspondência com os layers da rede do
  ; registro. Candidatos de outras tubulações só entram como fallback.
  (setq resultado nil)
  (foreach candidato candidatos
    (if (nth 6 candidato)
      (setq resultado (cons candidato resultado))
    )
  )
  (reverse resultado)
)

(defun geoauto-contexto-valor (contexto chave / item)
  (setq item (if contexto (assoc chave contexto) nil))
  (if item (cdr item) nil)
)

(defun geoauto-formatar-ponto-xdata (ponto)
  (if (and (listp ponto) (numberp (car ponto)) (numberp (cadr ponto)))
    (strcat
      (rtos (car ponto) 2 8) ","
      (rtos (cadr ponto) 2 8) ","
      (rtos (if (numberp (caddr ponto)) (caddr ponto) 0.0) 2 8)
    )
    ""
  )
)

(defun geoauto-construir-xdata
  (tipo registro-id codigo ponto execution-id / valores pares)
  (setq valores
    (list
      (strcat "TIPO=" (if tipo tipo ""))
      (strcat "REGISTRO=" (if registro-id registro-id ""))
      (strcat "CODIGO=" (if codigo codigo ""))
      (strcat "ORIGEM=" (geoauto-formatar-ponto-xdata ponto))
      (strcat "EXECUCAO=" (if execution-id execution-id ""))
    )
  )
  (setq pares (mapcar '(lambda (valor) (cons 1000 valor)) valores))
  (list -3 (append (list *geoauto-xdata-app*) pares))
)

(defun geoauto-aplicar-xdata
  (entidade tipo registro-id codigo ponto execution-id
   / ename dados xdata resultado)
  (regapp *geoauto-xdata-app*)
  (setq ename
    (cond
      ((= (type entidade) 'ENAME) entidade)
      ((= (type entidade) 'VLA-OBJECT)
        (vlax-vla-object->ename entidade)
      )
      (T nil)
    )
  )
  (if ename
    (progn
      (setq dados (entget ename '("*")))
      (setq xdata
        (geoauto-construir-xdata
          tipo registro-id codigo ponto execution-id
        )
      )
      (setq resultado
        (vl-catch-all-apply 'entmod (list (append dados (list xdata))))
      )
      (if (or (vl-catch-all-error-p resultado) (null resultado))
        nil
        (progn (entupd ename) T)
      )
    )
    nil
  )
)

(defun geoauto-entidade-gerada-p (entidade / ename dados)
  (setq ename
    (cond
      ((= (type entidade) 'ENAME) entidade)
      ((= (type entidade) 'VLA-OBJECT)
        (vlax-vla-object->ename entidade)
      )
      (T nil)
    )
  )
  (if ename
    (progn
      (setq dados (entget ename (list *geoauto-xdata-app*)))
      (if (assoc -3 dados) T nil)
    )
    nil
  )
)

(defun geoauto-xdata-contem-p (entidade prefixo / ename dados bloco item achou)
  (setq achou nil)
  (setq ename
    (cond
      ((= (type entidade) 'ENAME) entidade)
      ((= (type entidade) 'VLA-OBJECT)
        (vlax-vla-object->ename entidade)
      )
      (T nil)
    )
  )
  (if ename
    (progn
      (setq dados (entget ename (list *geoauto-xdata-app*)))
      (setq bloco (assoc -3 dados))
      (if bloco
        (foreach item (cdr (cadr bloco))
          (if (and (= (car item) 1000)
                   (= (substr (cdr item) 1 (strlen prefixo)) prefixo))
            (setq achou T)
          )
        )
      )
    )
  )
  achou
)

(defun geoauto-coletar-objetos-gerados (espaco / objetos entidade)
  (setq objetos nil)
  (vlax-for entidade espaco
    (if (and (geoauto-entidade-gerada-p entidade)
             (geoauto-xdata-contem-p
               entidade
               (strcat "EXECUCAO=" *geoauto-execution-id*)
             )
             (not (geoauto-xdata-contem-p entidade "TIPO=OBSTACULO")))
      (setq objetos (cons entidade objetos))
    )
  )
  objetos
)

(defun geoauto-remover-objetos-anteriores
  (espaco / objetos encontrados removidos falhas entidade resultado)
  (setq objetos (geoauto-coletar-objetos-gerados espaco))
  (setq encontrados (length objetos))
  (setq removidos 0)
  (setq falhas 0)
  (foreach entidade objetos
    (setq resultado (vl-catch-all-apply 'vla-Delete (list entidade)))
    (if (vl-catch-all-error-p resultado)
      (setq falhas (1+ falhas))
      (setq removidos (1+ removidos))
    )
  )
  (list encontrados removidos falhas)
)

(defun geoauto-contar-textos-usuario (espaco / quantidade entidade)
  (setq quantidade 0)
  (vlax-for entidade espaco
    (if (and (geoauto-tipo-texto-existente-p entidade)
             (not (geoauto-entidade-gerada-p entidade)))
      (setq quantidade (1+ quantidade))
    )
  )
  quantidade
)

(defun geoauto-valor-ponto-para-lista (valor / bruto)
  (setq bruto
    (if (= (type valor) 'VARIANT)
      (vlax-variant-value valor)
      valor
    )
  )
  (cond
    ((= (type bruto) 'SAFEARRAY)
      (vlax-safearray->list bruto)
    )
    ((listp bruto) bruto)
    (T nil)
  )
)

(defun geoauto-obter-bbox-vla (obj / ponto-min ponto-max lista-min lista-max)
  (vla-Update obj)
  (vla-GetBoundingBox obj 'ponto-min 'ponto-max)
  (setq lista-min (geoauto-valor-ponto-para-lista ponto-min))
  (setq lista-max (geoauto-valor-ponto-para-lista ponto-max))
  (if (and lista-min lista-max
           (numberp (car lista-min))
           (numberp (cadr lista-min))
           (numberp (car lista-max))
           (numberp (cadr lista-max)))
    (list
      (min (car lista-min) (car lista-max))
      (min (cadr lista-min) (cadr lista-max))
      (max (car lista-min) (car lista-max))
      (max (cadr lista-min) (cadr lista-max))
    )
    nil
  )
)

(defun geoauto-get-entity-bbox (entidade / obj resultado)
  (setq obj
    (cond
      ((= (type entidade) 'ENAME)
        (vlax-ename->vla-object entidade)
      )
      ((= (type entidade) 'VLA-OBJECT) entidade)
      (T nil)
    )
  )
  (if obj
    (progn
      (setq resultado
        (vl-catch-all-apply 'geoauto-obter-bbox-vla (list obj))
      )
      (if (vl-catch-all-error-p resultado) nil resultado)
    )
    nil
  )
)

(defun geoauto-expand-bbox (bbox margem)
  (if bbox
    (list
      (- (nth 0 bbox) margem)
      (- (nth 1 bbox) margem)
      (+ (nth 2 bbox) margem)
      (+ (nth 3 bbox) margem)
    )
    nil
  )
)

(defun geoauto-combine-bboxes (bbox-a bbox-b)
  (cond
    ((and bbox-a bbox-b)
      (list
        (min (nth 0 bbox-a) (nth 0 bbox-b))
        (min (nth 1 bbox-a) (nth 1 bbox-b))
        (max (nth 2 bbox-a) (nth 2 bbox-b))
        (max (nth 3 bbox-a) (nth 3 bbox-b))
      )
    )
    (bbox-a bbox-a)
    (bbox-b bbox-b)
    (T nil)
  )
)

(defun geoauto-bboxes-overlap-p (bbox-a bbox-b)
  (if (and bbox-a bbox-b)
    (not
      (or
        (< (nth 2 bbox-a) (nth 0 bbox-b))
        (> (nth 0 bbox-a) (nth 2 bbox-b))
        (< (nth 3 bbox-a) (nth 1 bbox-b))
        (> (nth 1 bbox-a) (nth 3 bbox-b))
      )
    )
    nil
  )
)

(defun geoauto-ponto-proximo-lista-p
  (ponto pontos tolerancia / encontrado ponto-existente)
  (setq encontrado nil)
  (foreach ponto-existente pontos
    (if (and
          (null encontrado)
          (<
            (geoauto-distancia-2d ponto ponto-existente)
            tolerancia))
      (setq encontrado T)
    )
  )
  encontrado
)

(defun geoauto-ponto-ocupado-por-tipo-p
  (tipo ponto ocupacoes tolerancia
   / encontrado ocupacao ponto-existente)
  (setq encontrado nil)
  (foreach ocupacao ocupacoes
    (setq ponto-existente (nth 1 ocupacao))
    (if (and
          (null encontrado)
          (= tipo (nth 0 ocupacao))
          (<
            (geoauto-distancia-2d ponto ponto-existente)
            tolerancia))
      (setq encontrado T)
    )
  )
  encontrado
)

(defun geoauto-tipo-estrutura-pluvial-layer (layer / chave)
  (setq chave (strcase (if layer layer "")))
  (cond
    ((= chave "GF-GRELHA") "GRELHA")
    ((= chave "GF-BOCA DE LOBO") "BOCA_LOBO")
    (T nil)
  )
)

(defun geoauto-coletar-estruturas-pluviais-existentes
  (space / ocupacoes entidade nome layer tipo fechada bbox centro)
  (setq ocupacoes nil)
  (vlax-for entidade space
    (setq nome
      (geoauto-chamada-segura 'vla-get-ObjectName (list entidade))
    )
    (setq layer
      (geoauto-chamada-segura 'vla-get-Layer (list entidade))
    )
    (setq tipo (geoauto-tipo-estrutura-pluvial-layer layer))
    (if (and (= nome "AcDbPolyline") tipo)
      (progn
        (setq fechada
          (geoauto-chamada-segura 'vla-get-Closed (list entidade))
        )
        (if (or (= fechada :vlax-true) (= fechada -1))
          (progn
            (setq bbox (geoauto-get-entity-bbox entidade))
            (if bbox
              (progn
                (setq centro
                  (list
                    (/ (+ (nth 0 bbox) (nth 2 bbox)) 2.0)
                    (/ (+ (nth 1 bbox) (nth 3 bbox)) 2.0)
                    0.0
                  )
                )
                (setq ocupacoes
                  (cons (list tipo centro) ocupacoes)
                )
              )
            )
          )
        )
      )
    )
  )
  ocupacoes
)

(defun geoauto-coletar-circulos-atencao-existentes
  (space / pontos entidade nome cor valor-centro centro)
  (setq pontos nil)
  (vlax-for entidade space
    (setq nome
      (geoauto-chamada-segura 'vla-get-ObjectName (list entidade))
    )
    (if (= nome "AcDbCircle")
      (progn
        (setq cor
          (geoauto-chamada-segura 'vla-get-Color (list entidade))
        )
        (if (= cor *geoauto-cor-atencao*)
          (progn
            (setq valor-centro
              (geoauto-chamada-segura 'vla-get-Center (list entidade))
            )
            (setq centro
              (if valor-centro
                (geoauto-variant-para-lista valor-centro)
                nil
              )
            )
            (if (and centro (numberp (car centro)) (numberp (cadr centro)))
              (setq pontos
                (cons
                  (list
                    (float (car centro))
                    (float (cadr centro))
                    0.0
                  )
                  pontos
                )
              )
            )
          )
        )
      )
    )
  )
  pontos
)

(defun geoauto-text-group-collision-p (bbox ocupados / colidiu ocupado)
  (setq colidiu nil)
  (foreach ocupado ocupados
    (if (and (not colidiu)
             (geoauto-bboxes-overlap-p bbox ocupado))
      (setq colidiu T)
    )
  )
  colidiu
)

(defun geoauto-contar-colisoes-bbox (bbox ocupados / quantidade ocupado)
  (setq quantidade 0)
  (if bbox
    (foreach ocupado ocupados
      (if (geoauto-bboxes-overlap-p bbox ocupado)
        (setq quantidade (1+ quantidade))
      )
    )
  )
  quantidade
)

(defun geoauto-register-text-bbox (bbox)
  (if bbox
    (setq *geoauto-text-occupied-boxes*
      (cons bbox *geoauto-text-occupied-boxes*)
    )
  )
  bbox
)

(defun geoauto-bbox-protecao-ponto (ponto raio)
  (if (and
        ponto
        (numberp (car ponto))
        (numberp (cadr ponto))
        (numberp raio)
        (> raio 0.0))
    (list
      (- (car ponto) raio)
      (- (cadr ponto) raio)
      (+ (car ponto) raio)
      (+ (cadr ponto) raio)
    )
    nil
  )
)

(defun geoauto-registrar-obstaculos-pv
  (referencias instrucoes raio margem
   / quantidade referencia familia bbox instrucao ponto)
  (setq quantidade 0)

  ; PVs que já existiam no desenho usam o BoundingBox real do bloco.
  (foreach referencia referencias
    (setq familia (geoauto-chave-familia-bloco (nth 3 referencia)))
    (setq bbox (nth 1 referencia))
    (if (and
          (eq (nth 2 referencia) 'BLOCO)
          (= familia "PV")
          bbox)
      (progn
        (setq *geoauto-text-occupied-boxes*
          (cons
            (geoauto-expand-bbox bbox margem)
            *geoauto-text-occupied-boxes*
          )
        )
        (setq quantidade (1+ quantidade))
      )
    )
  )

  ; PVs desta importação ainda serão inseridos depois dos textos. Uma zona
  ; quadrada conhecida protege antecipadamente o espaço ocupado pelo bloco.
  (foreach instrucao instrucoes
    (setq familia (geoauto-chave-familia-bloco (nth 1 instrucao)))
    (setq ponto (nth 4 instrucao))
    (if (and (= familia "PV") ponto)
      (progn
        (setq bbox (geoauto-bbox-protecao-ponto ponto raio))
        (if bbox
          (progn
            (setq *geoauto-text-occupied-boxes*
              (cons bbox *geoauto-text-occupied-boxes*)
            )
            (setq quantidade (1+ quantidade))
          )
        )
      )
    )
  )
  quantidade
)

(defun geoauto-apagar-entidade (entidade / resultado)
  (if entidade
    (progn
      (setq resultado
        (vl-catch-all-apply 'entdel (list entidade))
      )
      (if (vl-catch-all-error-p resultado) nil T)
    )
    T
  )
)

(defun geoauto-tipo-texto-existente-p (entidade / nome)
  (setq nome
    (geoauto-chamada-segura 'vla-get-ObjectName (list entidade))
  )
  (if (member nome '("AcDbText" "AcDbMText")) T nil)
)

(defun geoauto-coletar-bboxes-textos-existentes
  (espaco layers-validos margem / quantidade entidade bbox)
  ; layers-validos é mantido apenas para compatibilidade de assinatura.
  ; Por padrão nenhum TEXT/MTEXT comum participa da anticolisão. Quando a
  ; opção for habilitada futuramente, somente objetos com XData explícito
  ; TIPO=OBSTACULO serão considerados.
  (setq quantidade 0)
  (if *geoauto-considerar-textos-existentes-na-colisao*
    (vlax-for entidade espaco
      (if (and (geoauto-tipo-texto-existente-p entidade)
               (geoauto-xdata-contem-p entidade "TIPO=OBSTACULO"))
        (progn
          (setq bbox (geoauto-get-entity-bbox entidade))
          (if bbox
            (progn
              (geoauto-register-text-bbox
                (geoauto-expand-bbox bbox margem)
              )
              (setq quantidade (1+ quantidade))
            )
          )
        )
      )
    )
  )
  quantidade
)

(defun geoauto-ponto-dentro-bbox-p (ponto bbox)
  (if (and (listp ponto) bbox
           (numberp (car ponto))
           (numberp (cadr ponto)))
    (and
      (>= (car ponto) (nth 0 bbox))
      (<= (car ponto) (nth 2 bbox))
      (>= (cadr ponto) (nth 1 bbox))
      (<= (cadr ponto) (nth 3 bbox))
    )
    nil
  )
)

(defun geoauto-centro-bbox (bbox)
  (if bbox
    (list
      (/ (+ (nth 0 bbox) (nth 2 bbox)) 2.0)
      (/ (+ (nth 1 bbox) (nth 3 bbox)) 2.0)
      0.0
    )
    nil
  )
)

(defun geoauto-obter-bbox-curva-cache (curva / chave entrada bbox)
  (setq chave
    (geoauto-chamada-segura 'vla-get-Handle (list curva))
  )
  (setq entrada (if chave (assoc chave *geoauto-curve-bboxes*) nil))
  (if entrada
    (cdr entrada)
    (progn
      (setq bbox (geoauto-get-entity-bbox curva))
      (if (and chave bbox)
        (setq *geoauto-curve-bboxes*
          (cons (cons chave bbox) *geoauto-curve-bboxes*)
        )
      )
      bbox
    )
  )
)

(defun geoauto-criar-retangulo-bbox (bbox)
  (if bbox
    (entmakex
      (list
        '(0 . "LWPOLYLINE")
        '(100 . "AcDbEntity")
        (cons 8 "0")
        '(100 . "AcDbPolyline")
        (cons 90 4)
        (cons 70 1)
        (cons 10 (list (nth 0 bbox) (nth 1 bbox)))
        (cons 10 (list (nth 2 bbox) (nth 1 bbox)))
        (cons 10 (list (nth 2 bbox) (nth 3 bbox)))
        (cons 10 (list (nth 0 bbox) (nth 3 bbox)))
        (cons 210 '(0.0 0.0 1.0))
      )
    )
    nil
  )
)

(defun geoauto-converter-intersecoes-seguras (valor / resultado)
  (setq resultado
    (vl-catch-all-apply 'geoauto-valor-ponto-para-lista (list valor))
  )
  (if (vl-catch-all-error-p resultado) nil resultado)
)

(defun geoauto-curva-intersecta-bbox-p
  (curva bbox / bbox-curva centro ponto-proximo ponto-inicial ponto-final
   retangulo obj-retangulo intersecoes lista-intersecoes colide)
  (setq colide nil)
  (setq bbox-curva (geoauto-obter-bbox-curva-cache curva))
  (if (and bbox-curva (geoauto-bboxes-overlap-p bbox-curva bbox))
    (progn
      ; O ponto mais próximo do centro detecta curvas ou segmentos curtos
      ; inteiramente contidos na área do texto.
      (setq centro (geoauto-centro-bbox bbox))
      (setq ponto-proximo
        (geoauto-chamada-segura
          'vlax-curve-getClosestPointTo
          (list curva centro)
        )
      )
      (if (geoauto-ponto-dentro-bbox-p ponto-proximo bbox)
        (setq colide T)
      )

      (if (not colide)
        (progn
          (setq ponto-inicial
            (geoauto-chamada-segura 'vlax-curve-getStartPoint (list curva))
          )
          (setq ponto-final
            (geoauto-chamada-segura 'vlax-curve-getEndPoint (list curva))
          )
          (if (or (geoauto-ponto-dentro-bbox-p ponto-inicial bbox)
                  (geoauto-ponto-dentro-bbox-p ponto-final bbox))
            (setq colide T)
          )
        )
      )

      ; Para cruzamentos reais das bordas, usa IntersectWith sobre um
      ; retângulo temporário correspondente ao bounding box expandido.
      (if (not colide)
        (progn
          (setq retangulo (geoauto-criar-retangulo-bbox bbox))
          (if retangulo
            (progn
              (setq obj-retangulo (vlax-ename->vla-object retangulo))
              (setq intersecoes
                (vl-catch-all-apply
                  'vla-IntersectWith
                  (list curva obj-retangulo 0)
                )
              )
              (if (not (vl-catch-all-error-p intersecoes))
                (progn
                  (setq lista-intersecoes
                    (geoauto-converter-intersecoes-seguras intersecoes)
                  )
                  (if lista-intersecoes (setq colide T))
                )
              )
              (geoauto-apagar-entidade retangulo)
            )
          )
        )
      )
    )
  )
  colide
)

(defun geoauto-textos-interferem-curvas-p
  (bbox-profundidade bbox-diametro curvas margem
   / bbox-profundidade-seguro bbox-diametro-seguro interfere curva)
  ; Os boxes são avaliados separadamente. O box combinado não pode ser
  ; usado porque a tubulação deve passar justamente entre os dois textos.
  (setq bbox-profundidade-seguro
    (geoauto-expand-bbox bbox-profundidade margem)
  )
  (setq bbox-diametro-seguro
    (geoauto-expand-bbox bbox-diametro margem)
  )
  (setq interfere nil)
  (foreach curva curvas
    (if (and (not interfere)
             (or
               (and bbox-profundidade-seguro
                    (geoauto-curva-intersecta-bbox-p
                      curva bbox-profundidade-seguro
                    ))
               (and bbox-diametro-seguro
                    (geoauto-curva-intersecta-bbox-p
                      curva bbox-diametro-seguro
                    ))
             ))
      (setq interfere T)
    )
  )
  interfere
)

(defun geoauto-gerar-afastamentos-perpendiculares
  (passo limite-maximo / afastamentos valor)
  (setq afastamentos (list 0.0))
  (setq valor passo)
  (while (<= valor (+ limite-maximo 1.0e-9))
    (setq afastamentos (append afastamentos (list valor)))
    (setq valor (+ valor passo))
  )
  afastamentos
)

(defun geoauto-gerar-deslocamentos-candidatos
  (passo limite-maximo / deslocamentos valor)
  (setq deslocamentos (list 0.0))
  (setq valor passo)
  (while (<= valor (+ limite-maximo 1.0e-9))
    (setq deslocamentos
      (append deslocamentos (list valor (* -1.0 valor)))
    )
    (setq valor (+ valor passo))
  )
  deslocamentos
)

(defun geoauto-apagar-grupo-textos (grupo)
  (if grupo
    (progn
      (geoauto-apagar-entidade (nth 1 grupo))
      (geoauto-apagar-entidade (nth 2 grupo))
    )
  )
  nil
)

(defun geoauto-calcular-posicao-textos
  (contexto afastamento altura deslocamento afastamento-adicional
   / ponto-base tangente normal base-longitudinal afastamento-final
     ponto-profundidade ponto-diametro)
  (setq ponto-base (geoauto-contexto-valor contexto 'PONTO_BASE))
  (setq tangente (geoauto-contexto-valor contexto 'TANGENTE))
  (setq normal (geoauto-contexto-valor contexto 'NORMAL))
  (setq base-longitudinal
    (geoauto-ponto-deslocado-vetor ponto-base tangente deslocamento)
  )
  (setq afastamento-final (+ afastamento afastamento-adicional))
  ; Profundidade permanece no lado positivo da normal canônica.
  (setq ponto-profundidade
    (geoauto-ponto-deslocado-vetor
      base-longitudinal normal afastamento-final
    )
  )
  ; Diâmetro permanece no lado negativo da mesma normal. A altura é
  ; preservada para manter a tubulação entre as duas linhas-base.
  (setq ponto-diametro
    (geoauto-ponto-deslocado-vetor
      base-longitudinal normal (- (+ altura afastamento-final))
    )
  )
  (list base-longitudinal ponto-profundidade ponto-diametro)
)

(defun geoauto-criar-grupo-textos-contexto
  (layer estilo contexto afastamento altura
   texto-profundidade texto-diametro deslocamento margem
   afastamento-adicional
   / posicoes base-longitudinal ponto-profundidade ponto-diametro
     angulo-texto-final entidade-profundidade entidade-diametro
     bbox-profundidade bbox-diametro bbox-grupo fallback-bbox
     rotacao-profundidade rotacao-diametro rotacao-corresponde)
  (setq angulo-texto-final
    (geoauto-contexto-valor contexto 'ANGULO_TEXTO_FINAL)
  )
  (setq posicoes
    (geoauto-calcular-posicao-textos
      contexto afastamento altura deslocamento afastamento-adicional
    )
  )
  (setq base-longitudinal (nth 0 posicoes))
  (setq ponto-profundidade (nth 1 posicoes))
  (setq ponto-diametro (nth 2 posicoes))

  (setq entidade-profundidade
    (geoauto-chamada-segura
      'geoauto-inserir-texto
      (list
        layer estilo ponto-profundidade altura texto-profundidade
        angulo-texto-final "PROFUNDIDADE"
      )
    )
  )
  (if entidade-profundidade
    (progn
      (if texto-diametro
        (setq entidade-diametro
          (geoauto-chamada-segura
            'geoauto-inserir-texto
            (list
              layer estilo ponto-diametro altura texto-diametro
              angulo-texto-final "DIAMETRO"
            )
          )
        )
      )

      ; A escrita autoritativa por vla-AddText já aplicou e validou Rotation e
      ; DXF 50. Nenhuma rotina posterior pode reescrever a orientação.

      ; BoundingBox é exclusivamente auxiliar e fail-open.
      (setq bbox-profundidade
        (geoauto-get-entity-bbox entidade-profundidade)
      )
      (if entidade-diametro
        (setq bbox-diametro
          (geoauto-get-entity-bbox entidade-diametro)
        )
      )
      (setq bbox-grupo
        (geoauto-combine-bboxes bbox-profundidade bbox-diametro)
      )
      (setq fallback-bbox (if bbox-grupo nil T))
      (setq rotacao-profundidade
        (geoauto-obter-rotacao-texto entidade-profundidade)
      )
      (setq rotacao-diametro
        (if entidade-diametro
          (geoauto-obter-rotacao-texto entidade-diametro)
          nil
        )
      )
      (setq rotacao-corresponde
        (and
          (geoauto-rotacao-texto-corresponde-p
            entidade-profundidade angulo-texto-final
          )
          (or
            (null entidade-diametro)
            (geoauto-rotacao-texto-corresponde-p
              entidade-diametro angulo-texto-final
            )
          )
        )
      )
      (list
        'OK
        entidade-profundidade
        entidade-diametro
        (if bbox-grupo (geoauto-expand-bbox bbox-grupo margem) nil)
        bbox-profundidade
        bbox-diametro
        afastamento-adicional
        fallback-bbox
        base-longitudinal
        ponto-profundidade
        ponto-diametro
        rotacao-profundidade
        rotacao-diametro
        rotacao-corresponde
      )
    )
    (list
      'ERRO nil nil nil nil nil afastamento-adicional nil
      base-longitudinal ponto-profundidade ponto-diametro nil nil nil
    )
  )
)

(defun geoauto-avaliar-candidato-texto-contexto
  (layer estilo contexto afastamento altura
   texto-profundidade texto-diametro deslocamento margem
   afastamento-adicional curvas-interferencia margem-curva
   verificar-interferencia verificar-colisao-textos
   / criado bbox resultado-verificacao colisoes-texto colisoes-curva
     falha-verificacao)
  (setq criado
    (geoauto-criar-grupo-textos-contexto
      layer estilo contexto afastamento altura
      texto-profundidade texto-diametro deslocamento margem
      afastamento-adicional
    )
  )
  (if (eq (nth 0 criado) 'OK)
    (progn
      (setq bbox (nth 3 criado))
      (setq falha-verificacao nil)
      (setq colisoes-texto 0)
      (setq colisoes-curva 0)

      (if (and bbox verificar-colisao-textos)
        (progn
          (setq resultado-verificacao
            (vl-catch-all-apply
              'geoauto-contar-colisoes-bbox
              (list bbox *geoauto-text-occupied-boxes*)
            )
          )
          (if (vl-catch-all-error-p resultado-verificacao)
            (setq falha-verificacao T)
            (setq colisoes-texto resultado-verificacao)
          )
        )
      )

      (if (and bbox verificar-interferencia)
        (progn
          (setq resultado-verificacao
            (vl-catch-all-apply
              'geoauto-textos-interferem-curvas-p
              (list
                (nth 4 criado)
                (nth 5 criado)
                curvas-interferencia
                margem-curva
              )
            )
          )
          (if (vl-catch-all-error-p resultado-verificacao)
            (setq falha-verificacao T)
            (if resultado-verificacao (setq colisoes-curva 1))
          )
        )
      )

      ; Falha de verificação é fail-open: os textos permanecem candidatos,
      ; com a rotação já fixada pelo contexto.
      (append
        criado
        (list colisoes-texto colisoes-curva falha-verificacao)
      )
    )
    criado
  )
)

(defun geoauto-selecionar-afastamento-perpendicular-contexto
  (layer estilo contexto afastamento altura
   texto-profundidade texto-diametro passo-perpendicular
   max-afastamento-perpendicular curvas-interferencia margem
   margem-curva verificar-interferencia
   / afastamentos afastamento-adicional resultado aceito escolhido
     melhor-afastamento melhor-colisoes colisoes-curva
     tentativas interferencias forcado bbox-falhas falhas-avaliacao
     tentativas-rotacao)
  (setq afastamentos
    (geoauto-gerar-afastamentos-perpendiculares
      passo-perpendicular max-afastamento-perpendicular
    )
  )
  (setq aceito nil)
  (setq escolhido 0.0)
  (setq melhor-afastamento nil)
  (setq melhor-colisoes nil)
  (setq tentativas 0)
  (setq interferencias 0)
  (setq forcado nil)
  (setq bbox-falhas 0)
  (setq falhas-avaliacao 0)
  (setq tentativas-rotacao 0)

  ; ETAPA 6: resolve somente o afastamento perpendicular na posição-base.
  ; Nenhum deslocamento longitudinal participa desta decisão.
  (foreach afastamento-adicional afastamentos
    (if (not aceito)
      (progn
        (if (> afastamento-adicional 0.0)
          (setq tentativas (1+ tentativas))
        )
        (setq resultado
          (vl-catch-all-apply
            'geoauto-avaliar-candidato-texto-contexto
            (list
              layer estilo contexto afastamento altura
              texto-profundidade texto-diametro 0.0 margem
              afastamento-adicional curvas-interferencia margem-curva
              verificar-interferencia nil
            )
          )
        )
        (if (vl-catch-all-error-p resultado)
          (setq falhas-avaliacao (1+ falhas-avaliacao))
          (if (eq (nth 0 resultado) 'OK)
            (progn
              (if (nth 7 resultado)
                (setq bbox-falhas (1+ bbox-falhas))
              )
              (if (and
                    (geoauto-contexto-rotacao-resolvida-p contexto)
                    (nth 13 resultado))
                (setq tentativas-rotacao (1+ tentativas-rotacao))
              )
              (setq colisoes-curva (nth 15 resultado))
              (setq interferencias (+ interferencias colisoes-curva))
              (geoauto-apagar-grupo-textos resultado)
              (if (= colisoes-curva 0)
                (progn
                  (setq escolhido afastamento-adicional)
                  (setq aceito T)
                )
                (if (or
                      (null melhor-colisoes)
                      (< colisoes-curva melhor-colisoes)
                      (and
                        (= colisoes-curva melhor-colisoes)
                        (< afastamento-adicional melhor-afastamento)))
                  (progn
                    (setq melhor-colisoes colisoes-curva)
                    (setq melhor-afastamento afastamento-adicional)
                  )
                )
              )
            )
          )
        )
      )
    )
  )

  (if (not aceito)
    (progn
      (setq forcado T)
      (setq escolhido
        (if (numberp melhor-afastamento) melhor-afastamento 0.0)
      )
    )
  )
  (list
    escolhido tentativas interferencias forcado bbox-falhas
    falhas-avaliacao tentativas-rotacao
  )
)

(defun geoauto-inserir-contexto-fallback-visual
  (layer estilo contexto afastamento altura
   texto-profundidade texto-diametro margem afastamento-adicional
   motivo / criado rotacao-criada rotacao-preservada)
  (setq criado
    (geoauto-criar-grupo-textos-contexto
      layer estilo contexto afastamento altura
      texto-profundidade texto-diametro 0.0 margem
      afastamento-adicional
    )
  )
  (if (eq (nth 0 criado) 'OK)
    (progn
      (if (nth 3 criado) (geoauto-register-text-bbox (nth 3 criado)))
      (setq rotacao-criada
        (if (numberp (nth 11 criado)) (nth 11 criado) (nth 12 criado))
      )
      (setq rotacao-preservada
        (and
          (geoauto-contexto-rotacao-resolvida-p contexto)
          (nth 13 criado)
        )
      )
      (list
        'OK (nth 1 criado) (nth 2 criado) 0.0
        0 0 T (nth 3 criado)
        afastamento-adicional 0 0 nil (nth 7 criado)
        T 1 rotacao-criada (nth 8 criado) motivo
        rotacao-preservada
        (if (nth 7 criado) 1 0)
        (if (and
              (geoauto-contexto-rotacao-resolvida-p contexto)
              (nth 13 criado))
          1
          0
        )
        (nth 13 criado)
        (geoauto-contexto-valor contexto 'STATUS)
        (geoauto-contexto-valor contexto 'ANGULO_GEOMETRICO)
        (geoauto-contexto-valor contexto 'ANGULO_TEXTO_FINAL)
      )
    )
    (list
      'ERRO nil nil 0.0 0 0 T nil afastamento-adicional 0 0 nil nil
      T 1 nil
      (geoauto-contexto-valor contexto 'PONTO_BASE)
      motivo nil 0 0 nil
      (geoauto-contexto-valor contexto 'STATUS)
      (geoauto-contexto-valor contexto 'ANGULO_GEOMETRICO)
      (geoauto-contexto-valor contexto 'ANGULO_TEXTO_FINAL)
    )
  )
)

(defun geoauto-inserir-grupo-textos-contexto
  (layer estilo contexto afastamento altura
   texto-profundidade texto-diametro passo-longitudinal
   max-deslocamento fator-margem curvas-interferencia
   passo-perpendicular max-afastamento-perpendicular
   fator-margem-curva verificar-interferencia verificar-colisao-textos
   / margem margem-curva selecao afastamento-adicional
     tentativas-perpendiculares interferencias-curvas forcado-curva
     bbox-falhas falhas-avaliacao tentativas-rotacao candidatos
     deslocamento resultado aceito melhor melhor-colisoes melhor-distancia
     candidato-colisoes candidato-distancia deslocamento-aceito
     tentativas-reposicionamento colisoes forcado fallback-bbox
     grupo-final motivo fallback-visual rotacao-criada ponto-final
     rotacao-preservada rotacao-corresponde)
  (setq margem (* altura fator-margem))
  (setq margem-curva (* altura fator-margem-curva))

  ; ETAPA 6 é concluída antes da anticolisão longitudinal.
  (setq selecao
    (geoauto-selecionar-afastamento-perpendicular-contexto
      layer estilo contexto afastamento altura
      texto-profundidade texto-diametro passo-perpendicular
      max-afastamento-perpendicular curvas-interferencia margem
      margem-curva verificar-interferencia
    )
  )
  (setq afastamento-adicional (nth 0 selecao))
  (setq tentativas-perpendiculares (nth 1 selecao))
  (setq interferencias-curvas (nth 2 selecao))
  (setq forcado-curva (nth 3 selecao))
  (setq bbox-falhas (nth 4 selecao))
  (setq falhas-avaliacao (nth 5 selecao))
  (setq tentativas-rotacao (nth 6 selecao))

  ; ETAPA 7 altera exclusivamente o deslocamento longitudinal.
  (setq candidatos
    (geoauto-gerar-deslocamentos-candidatos
      passo-longitudinal max-deslocamento
    )
  )
  (setq aceito nil)
  (setq melhor nil)
  (setq melhor-colisoes nil)
  (setq melhor-distancia nil)
  (setq deslocamento-aceito 0.0)
  (setq tentativas-reposicionamento 0)
  (setq colisoes 0)
  (setq forcado nil)

  (foreach deslocamento candidatos
    (if (not aceito)
      (progn
        (if (/= deslocamento 0.0)
          (setq tentativas-reposicionamento
            (1+ tentativas-reposicionamento)
          )
        )
        (setq resultado
          (vl-catch-all-apply
            'geoauto-avaliar-candidato-texto-contexto
            (list
              layer estilo contexto afastamento altura
              texto-profundidade texto-diametro deslocamento margem
              afastamento-adicional curvas-interferencia margem-curva
              verificar-interferencia verificar-colisao-textos
            )
          )
        )
        (if (vl-catch-all-error-p resultado)
          (setq falhas-avaliacao (1+ falhas-avaliacao))
          (if (eq (nth 0 resultado) 'OK)
            (progn
              (if (nth 7 resultado)
                (setq bbox-falhas (1+ bbox-falhas))
              )
              (if (and
                    (geoauto-contexto-rotacao-resolvida-p contexto)
                    (nth 13 resultado))
                (setq tentativas-rotacao (1+ tentativas-rotacao))
              )
              (setq candidato-colisoes
                (+ (nth 14 resultado) (nth 15 resultado))
              )
              (setq candidato-distancia (abs deslocamento))
              (setq colisoes (+ colisoes (nth 14 resultado)))
              (setq interferencias-curvas
                (+ interferencias-curvas (nth 15 resultado))
              )
              (if (= candidato-colisoes 0)
                (progn
                  (if melhor (geoauto-apagar-grupo-textos melhor))
                  (setq aceito resultado)
                  (setq deslocamento-aceito deslocamento)
                )
                (if (or
                      (null melhor-colisoes)
                      (< candidato-colisoes melhor-colisoes)
                      (and
                        (= candidato-colisoes melhor-colisoes)
                        (< candidato-distancia melhor-distancia)))
                  (progn
                    (if melhor (geoauto-apagar-grupo-textos melhor))
                    (setq melhor resultado)
                    (setq melhor-colisoes candidato-colisoes)
                    (setq melhor-distancia candidato-distancia)
                    (setq deslocamento-aceito deslocamento)
                  )
                  (geoauto-apagar-grupo-textos resultado)
                )
              )
            )
          )
        )
      )
    )
  )

  (cond
    (aceito
      (setq grupo-final aceito)
    )
    (melhor
      (setq grupo-final melhor)
      (setq forcado T)
    )
    (T
      (setq grupo-final nil)
    )
  )

  (if grupo-final
    (progn
      (setq fallback-bbox (nth 7 grupo-final))
      (if (nth 3 grupo-final)
        (geoauto-register-text-bbox (nth 3 grupo-final))
      )
      (setq rotacao-criada
        (if (numberp (nth 11 grupo-final))
          (nth 11 grupo-final)
          (nth 12 grupo-final)
        )
      )
      (setq ponto-final (nth 8 grupo-final))
      (setq rotacao-corresponde (nth 13 grupo-final))
      (setq fallback-visual
        (or forcado fallback-bbox (> falhas-avaliacao 0))
      )
      (setq motivo
        (cond
          ((> falhas-avaliacao 0) 'FALHA_ANTICOLISAO_FAIL_OPEN)
          (fallback-bbox 'FALHA_BOUNDING_BOX_FAIL_OPEN)
          (forcado 'POSICIONAMENTO_MENOR_CONFLITO)
          ((/= deslocamento-aceito 0.0) 'ANTICOLISAO_LONGITUDINAL)
          ((> afastamento-adicional 0.0) 'AFASTAMENTO_PERPENDICULAR)
          (T 'POSICAO_BASE)
        )
      )
      (setq rotacao-preservada
        (and
          fallback-visual
          (geoauto-contexto-rotacao-resolvida-p contexto)
          rotacao-corresponde
        )
      )
      (list
        'OK
        (nth 1 grupo-final)
        (nth 2 grupo-final)
        deslocamento-aceito
        tentativas-reposicionamento
        colisoes
        forcado
        (nth 3 grupo-final)
        afastamento-adicional
        tentativas-perpendiculares
        interferencias-curvas
        forcado-curva
        fallback-bbox
        fallback-visual
        falhas-avaliacao
        rotacao-criada
        ponto-final
        motivo
        rotacao-preservada
        bbox-falhas
        tentativas-rotacao
        rotacao-corresponde
        (geoauto-contexto-valor contexto 'STATUS)
        (geoauto-contexto-valor contexto 'ANGULO_GEOMETRICO)
        (geoauto-contexto-valor contexto 'ANGULO_TEXTO_FINAL)
      )
    )
    ; Nenhuma avaliação visual pôde produzir um grupo. Insere diretamente
    ; na posição-base com o mesmo contexto de orientação.
    (geoauto-inserir-contexto-fallback-visual
      layer estilo contexto afastamento altura
      texto-profundidade texto-diametro margem afastamento-adicional
      'FALLBACK_VISUAL_POSICAO_BASE
    )
  )
)


(defun geoauto-distancia-ponto-bbox (ponto bbox / dx dy)
  (if (and ponto bbox)
    (progn
      (setq dx
        (cond
          ((< (car ponto) (nth 0 bbox)) (- (nth 0 bbox) (car ponto)))
          ((> (car ponto) (nth 2 bbox)) (- (car ponto) (nth 2 bbox)))
          (T 0.0)
        )
      )
      (setq dy
        (cond
          ((< (cadr ponto) (nth 1 bbox)) (- (nth 1 bbox) (cadr ponto)))
          ((> (cadr ponto) (nth 3 bbox)) (- (cadr ponto) (nth 3 bbox)))
          (T 0.0)
        )
      )
      (sqrt (+ (* dx dx) (* dy dy)))
    )
    nil
  )
)

(defun geoauto-entidade-diagnostico-menor-p (a b)
  (< (nth 0 a) (nth 0 b))
)

(defun geoauto-encontrar-entidade-diagnostico
  (ponto entidades / resultados entidade ponto-proximo distancia layer
   nome-objeto handle bbox distancia-bbox status)
  (setq resultados nil)
  (foreach entidade entidades
    (setq ponto-proximo
      (geoauto-chamada-segura
        'vlax-curve-getClosestPointTo
        (list entidade ponto)
      )
    )
    (setq status 'ERRO_CLOSEST_POINT)
    (setq distancia nil)
    (if (and ponto-proximo (listp ponto-proximo))
      (progn
        (setq distancia (geoauto-distancia-2d ponto ponto-proximo))
        (setq status 'OK_CLOSEST_POINT)
      )
      (progn
        ; O BoundingBox é usado apenas para explicar uma entidade não
        ; suportada. Nunca participa da associação geométrica.
        (setq bbox (geoauto-get-entity-bbox entidade))
        (setq distancia-bbox (geoauto-distancia-ponto-bbox ponto bbox))
        (if (numberp distancia-bbox)
          (progn
            (setq distancia distancia-bbox)
            (setq status 'APROXIMACAO_BBOX_DIAGNOSTICA)
          )
        )
      )
    )
    (if (numberp distancia)
      (progn
        (setq layer
          (geoauto-chamada-segura 'vla-get-Layer (list entidade))
        )
        (setq nome-objeto
          (geoauto-chamada-segura 'vla-get-ObjectName (list entidade))
        )
        (setq handle (geoauto-handle-entidade entidade))
        (setq resultados
          (cons
            (list distancia entidade layer nome-objeto handle status)
            resultados
          )
        )
      )
    )
  )
  (car (vl-sort resultados 'geoauto-entidade-diagnostico-menor-p))
)

(defun geoauto-encontrar-referencia-proxima
  (ponto referencias / resultados referencia distancia)
  (setq resultados nil)
  (foreach referencia referencias
    (setq distancia
      (geoauto-distancia-ponto-bbox ponto (nth 1 referencia))
    )
    (if (numberp distancia)
      (setq resultados (cons (cons distancia referencia) resultados))
    )
  )
  (car
    (vl-sort
      resultados
      '(lambda (a b) (< (car a) (car b)))
    )
  )
)

(defun geoauto-classificar-motivo-fallback
  (ponto status layers-validos raio-maximo curvas-abertas
   curvas-fechadas-por-layer entidades-nao-suportadas-por-layer
   referencias-diagnostico
   / curvas-fechadas entidades-nao-suportadas fechado-proximo
     nao-suportada-proxima referencia-proxima global-proxima
     motivo distancia-global layer-global tipo-global handle-global
     distancia-fechada tipo-nao-suportado tipo-referencia nome-referencia)
  (setq motivo 'ERRO_DESCONHECIDO)
  (setq distancia-global nil)
  (setq layer-global nil)
  (setq tipo-global nil)
  (setq handle-global nil)
  (setq distancia-fechada nil)
  (setq tipo-nao-suportado nil)
  (setq tipo-referencia nil)
  (setq nome-referencia nil)

  (cond
    ((eq status 'SEM-LAYER)
      (setq motivo 'SEM_LAYERS_CONFIGURADOS)
    )
    ((eq status 'FORA-RAIO)
      (setq motivo 'CURVA_FORA_DE_1M)
    )
    ((eq status 'ERRO_CLOSEST_POINT)
      (setq motivo 'ERRO_CLOSEST_POINT)
    )
    ((eq status 'ROTACAO-DESATIVADA)
      (setq motivo 'ERRO_DESCONHECIDO)
    )
    (T
      (setq curvas-fechadas
        (geoauto-obter-curvas-por-layers
          curvas-fechadas-por-layer layers-validos
        )
      )
      (setq fechado-proximo
        (geoauto-encontrar-entidade-diagnostico ponto curvas-fechadas)
      )
      (if fechado-proximo
        (setq distancia-fechada (nth 0 fechado-proximo))
      )

      (setq entidades-nao-suportadas
        (geoauto-obter-curvas-por-layers
          entidades-nao-suportadas-por-layer layers-validos
        )
      )
      (setq nao-suportada-proxima
        (geoauto-encontrar-entidade-diagnostico
          ponto entidades-nao-suportadas
        )
      )
      (if nao-suportada-proxima
        (setq tipo-nao-suportado (nth 3 nao-suportada-proxima))
      )

      (setq referencia-proxima
        (geoauto-encontrar-referencia-proxima ponto referencias-diagnostico)
      )
      (if referencia-proxima
        (progn
          (setq tipo-referencia (nth 3 referencia-proxima))
          (setq nome-referencia (nth 4 referencia-proxima))
        )
      )

      (setq global-proxima
        (geoauto-encontrar-entidade-diagnostico ponto curvas-abertas)
      )
      (if global-proxima
        (progn
          (setq distancia-global (nth 0 global-proxima))
          (setq layer-global (nth 2 global-proxima))
          (setq tipo-global (nth 3 global-proxima))
          (setq handle-global (nth 4 global-proxima))
        )
      )

      (cond
        ((and fechado-proximo
              (<= (nth 0 fechado-proximo) raio-maximo))
          (setq motivo 'CURVA_FECHADA_IGNORADA)
        )
        ((and nao-suportada-proxima
              (<= (nth 0 nao-suportada-proxima) raio-maximo))
          (setq motivo 'ENTIDADE_NAO_SUPORTADA)
        )
        ((and referencia-proxima
              (<= (car referencia-proxima) raio-maximo)
              (eq (nth 3 referencia-proxima) 'XREF))
          (setq motivo 'GEOMETRIA_EM_XREF)
        )
        ((and referencia-proxima
              (<= (car referencia-proxima) raio-maximo))
          (setq motivo 'GEOMETRIA_EM_BLOCO)
        )
        ((and global-proxima
              (<= (nth 0 global-proxima) raio-maximo)
              (vl-string-search "|" (if layer-global layer-global ""))
              (geoauto-layer-valido-p layer-global layers-validos))
          (setq motivo 'LAYER_PREFIXADO_POR_XREF)
        )
        ((and global-proxima
              (<= (nth 0 global-proxima) raio-maximo)
              (not (geoauto-layer-valido-p layer-global layers-validos)))
          (setq motivo 'LAYER_NAO_MAPEADO)
        )
        (T
          (setq motivo 'NENHUMA_CURVA_NO_LAYER)
        )
      )
    )
  )

  (list
    motivo distancia-global layer-global tipo-global handle-global
    distancia-fechada tipo-nao-suportado tipo-referencia nome-referencia
  )
)

(defun geoauto-rede-agua-p (rede)
  (= (geoauto-normalizar-layer rede)
     (geoauto-normalizar-layer "Água potável"))
)


(defun geoauto-contador-incrementar (chave contadores / item)
  (setq item (assoc chave contadores))
  (if item
    (subst (cons chave (1+ (cdr item))) item contadores)
    (cons (cons chave 1) contadores)
  )
)

(defun geoauto-contador-valor (item)
  (if (numberp (cdr item)) (cdr item) (cadr item))
)

(defun geoauto-imprimir-contadores (titulo contadores / item)
  (if contadores
    (progn
      (princ (strcat "\n" titulo))
      (foreach item contadores
        (princ
          (strcat
            "\n  - " (car item) ": "
            (itoa (geoauto-contador-valor item))
          )
        )
      )
    )
  )
)

(defun geoauto-nome-arquivo (caminho / base extensao)
  (if caminho
    (progn
      (setq base (vl-filename-base caminho))
      (setq extensao (vl-filename-extension caminho))
      (strcat (if base base caminho) (if extensao extensao ""))
    )
    "<arquivo não informado>"
  )
)

(defun geo:block-loaded-p (block-name)
  (if (tblsearch "BLOCK" block-name) T nil)
)

(defun geo:resolve-block-source (block-name block-file / encontrado)
  (cond
    ((geo:block-loaded-p block-name) block-name)
    ((setq encontrado (findfile block-file)) encontrado)
    (T nil)
  )
)

(defun geoauto-coordenadas-2d-vertices
  (vertices / valores ponto)
  (setq valores nil)
  (foreach ponto vertices
    (setq valores
      (append valores (list (float (car ponto)) (float (cadr ponto))))
    )
  )
  valores
)

(defun geoauto-inserir-estrutura-pluvial
  (space estrutura
   / tipo layer vertices centro codigo registro-id valores coordenadas
     obj resultado-layer resultado-fechada resultado-xdata)
  (setq tipo (nth 0 estrutura))
  (setq layer (nth 1 estrutura))
  (setq vertices (nth 2 estrutura))
  (setq centro (nth 3 estrutura))
  (setq codigo (nth 4 estrutura))
  (setq registro-id (nth 5 estrutura))
  (setq valores (geoauto-coordenadas-2d-vertices vertices))
  (setq coordenadas
    (vlax-make-safearray
      vlax-vbDouble
      (cons 0 (1- (length valores)))
    )
  )
  (vlax-safearray-fill coordenadas valores)
  (setq obj
    (vl-catch-all-apply
      'vla-AddLightWeightPolyline
      (list space coordenadas)
    )
  )
  (if (vl-catch-all-error-p obj)
    nil
    (progn
      (setq resultado-layer
        (vl-catch-all-apply 'vla-put-Layer (list obj layer))
      )
      (setq resultado-fechada
        (vl-catch-all-apply
          'vla-put-Closed
          (list obj :vlax-true)
        )
      )
      (setq resultado-xdata
        (if (and
              (not (vl-catch-all-error-p resultado-layer))
              (not (vl-catch-all-error-p resultado-fechada)))
          (geoauto-aplicar-xdata
            obj "ESTRUTURA_PLUVIAL" registro-id codigo centro
            *geoauto-execution-id*
          )
          nil
        )
      )
      (if resultado-xdata
        (progn
          (vl-catch-all-apply 'vla-Update (list obj))
          obj
        )
        (progn
          (vl-catch-all-apply 'vla-Delete (list obj))
          nil
        )
      )
    )
  )
)

(defun geoauto-inserir-circulo-atencao
  (space ponto layer registro-id codigo
   / obj resultado-layer resultado-cor resultado-xdata)
  (if
    (geoauto-ponto-proximo-lista-p
      ponto
      *geoauto-pontos-circulos-atencao*
      *geoauto-tolerancia-sobreposicao-blocos*
    )
    'SOBREPOSICAO
    (progn
      (setq obj
        (vl-catch-all-apply
          'vla-AddCircle
          (list space (vlax-3d-point ponto) *geoauto-raio-atencao*)
        )
      )
      (if (vl-catch-all-error-p obj)
        nil
        (progn
          (setq resultado-layer
            (vl-catch-all-apply 'vla-put-Layer (list obj layer))
          )
          (setq resultado-cor
            (vl-catch-all-apply
              'vla-put-Color
              (list obj *geoauto-cor-atencao*)
            )
          )
          (setq resultado-xdata
            (if (and
                  (not (vl-catch-all-error-p resultado-layer))
                  (not (vl-catch-all-error-p resultado-cor)))
              (geoauto-aplicar-xdata
                obj "ATENCAO" registro-id codigo ponto
                *geoauto-execution-id*
              )
              nil
            )
          )
          (if resultado-xdata
            (progn
              (setq *geoauto-pontos-circulos-atencao*
                (cons ponto *geoauto-pontos-circulos-atencao*)
              )
              obj
            )
            (progn
              (vl-catch-all-apply 'vla-Delete (list obj))
              nil
            )
          )
        )
      )
    )
  )
)

(defun geoauto-inserir-anotacao-divisa
  (anotacao estilo altura
   / layer ponto texto codigo registro-id cor entidade resultado-cor)
  (setq layer (nth 0 anotacao))
  (setq ponto (nth 1 anotacao))
  (setq texto (nth 2 anotacao))
  (setq codigo (nth 3 anotacao))
  (setq registro-id (nth 4 anotacao))
  (setq cor (nth 5 anotacao))
  (setq *geoauto-registro-id-atual* registro-id)
  (setq *geoauto-codigo-atual* codigo)
  (setq *geoauto-ponto-original-atual* ponto)
  (setq entidade
    (geoauto-inserir-texto
      layer estilo ponto altura texto 0.0 "ANOTACAO_DIVISA"
    )
  )
  (if entidade
    (progn
      (setq resultado-cor
        (vl-catch-all-apply
          'vla-put-Color
          (list (vlax-ename->vla-object entidade) cor)
        )
      )
      (if (vl-catch-all-error-p resultado-cor)
        (progn
          (vl-catch-all-apply 'entdel (list entidade))
          nil
        )
        entidade
      )
    )
    nil
  )
)

(defun geo:calcular-rotacao-bloco
  (modo rotacao-fixa ponto curvas-por-layer layers-validos
   raio-primario raio-secundario raio-maximo tolerancia-ambiguidade
   distancia-amostra / orientacao angulo-fallback)
  (if (= (strcase modo) "FOLLOW_NETWORK")
    (progn
      ; Blocos reutilizam o mesmo resolvedor geométrico V2.1.7, mas preservam
      ; sua política histórica independente de rotação fixa como fallback.
      (setq orientacao
        (geoauto-v214-resolver-orientacao
          ponto curvas-por-layer layers-validos "BLOCO" nil nil)
      )
      (if (geoauto-contexto-rotacao-resolvida-p orientacao)
        (list
          (geoauto-v214-valor orientacao 'READABLE_ANGLE_RAD) T nil
          (length (geoauto-v214-valor orientacao 'CANDIDATES_REJECTED))
          (geoauto-v214-valor orientacao 'DISTANCE_2D)
          (geoauto-v214-valor orientacao 'SEARCH_RADIUS) nil nil
          (geoauto-v214-valor orientacao 'SOURCE_LAYER) 'OK
        )
        (progn
          (setq angulo-fallback (geoauto-normalizar-angulo-cad rotacao-fixa))
          (list
            angulo-fallback nil T
            (length (geoauto-v214-valor orientacao 'CANDIDATES_REJECTED))
            (geoauto-v214-valor orientacao 'DISTANCE_2D)
            nil nil nil nil (geoauto-v214-valor orientacao 'ERROR_CODE)))
      )
    )
    (list
      (geoauto-normalizar-angulo-cad rotacao-fixa) nil nil
      0 nil nil nil nil nil 'FIXED
    )
  )
)

(defun geo:insert-block
  (space instrucao curvas-por-layer
   raio-primario raio-secundario raio-maximo tolerancia-ambiguidade
   distancia-amostra pontos-blocos-ocupados tolerancia-sobreposicao
   / block-key block-name block-file layer ponto escala-x escala-y
     escala-z modo-rotacao rotacao-fixa codigo registro-id layers-validos
     source dados-rotacao angulo obj resultado-layer resultado-xdata
     sobreposto)
  (setq block-key (nth 0 instrucao))
  (setq block-name (nth 1 instrucao))
  (setq block-file (nth 2 instrucao))
  (setq layer (nth 3 instrucao))
  (setq ponto (nth 4 instrucao))
  (setq escala-x (nth 5 instrucao))
  (setq escala-y (nth 6 instrucao))
  (setq escala-z (nth 7 instrucao))
  (setq modo-rotacao (nth 8 instrucao))
  (setq rotacao-fixa (nth 9 instrucao))
  (setq codigo (nth 11 instrucao))
  (setq registro-id (nth 14 instrucao))
  (setq layers-validos (nth 15 instrucao))
  (setq sobreposto
    (geoauto-ponto-ocupado-por-bloco-p
      block-name ponto pontos-blocos-ocupados tolerancia-sobreposicao
    )
  )

  (if sobreposto
    (list 'SOBREPOSICAO block-key nil nil block-file 0 nil nil 0 nil nil nil)
    (progn
      (setq source (geo:resolve-block-source block-name block-file))
      (if (null source)
        (list 'ARQUIVO-AUSENTE block-key nil nil block-file 0 nil nil 0 nil nil nil)
        (progn
      (setq dados-rotacao
        (geo:calcular-rotacao-bloco
          modo-rotacao rotacao-fixa ponto curvas-por-layer layers-validos
          raio-primario raio-secundario raio-maximo
          tolerancia-ambiguidade distancia-amostra
        )
      )
      (setq angulo (nth 0 dados-rotacao))
      (setq obj
        (vl-catch-all-apply
          'vla-InsertBlock
          (list
            space (vlax-3d-point ponto) source
            escala-x escala-y escala-z angulo
          )
        )
      )
      (if (vl-catch-all-error-p obj)
        (list
          'ERRO block-key nil (nth 2 dados-rotacao) block-file
          (nth 3 dados-rotacao) (nth 4 dados-rotacao)
          (nth 5 dados-rotacao) (if (nth 6 dados-rotacao) 1 0)
          (nth 7 dados-rotacao) (nth 8 dados-rotacao)
          (nth 9 dados-rotacao)
        )
        (progn
          ; O ponto do BlockReference permanece exatamente no CSV.
          (setq resultado-layer
            (vl-catch-all-apply 'vla-put-Layer (list obj layer))
          )
          (if (vl-catch-all-error-p resultado-layer)
            (progn
              (vl-catch-all-apply 'vla-Delete (list obj))
              (list
                'ERRO block-key nil (nth 2 dados-rotacao) block-file
                (nth 3 dados-rotacao) (nth 4 dados-rotacao)
                (nth 5 dados-rotacao) (if (nth 6 dados-rotacao) 1 0)
                (nth 7 dados-rotacao) (nth 8 dados-rotacao)
                (nth 9 dados-rotacao)
              )
            )
            (progn
              (setq resultado-xdata
                (geoauto-aplicar-xdata
                  obj "BLOCO" registro-id codigo ponto
                  *geoauto-execution-id*
                )
              )
              (if resultado-xdata
                (list
                  'OK block-key (nth 1 dados-rotacao)
                  (nth 2 dados-rotacao) block-file
                  (nth 3 dados-rotacao) (nth 4 dados-rotacao)
                  (nth 5 dados-rotacao) (if (nth 6 dados-rotacao) 1 0)
                  (nth 7 dados-rotacao) (nth 8 dados-rotacao)
                  (nth 9 dados-rotacao)
                )
                (progn
                  ; Não mantém bloco sem identificação persistente, pois ele
                  ; não poderia ser substituído com segurança na próxima execução.
                  (vl-catch-all-apply 'vla-Delete (list obj))
                  (list
                    'ERRO-XDATA block-key nil (nth 2 dados-rotacao)
                    block-file (nth 3 dados-rotacao) (nth 4 dados-rotacao)
                    (nth 5 dados-rotacao) (if (nth 6 dados-rotacao) 1 0)
                    (nth 7 dados-rotacao) (nth 8 dados-rotacao)
                    (nth 9 dados-rotacao)
                  )
                )
              )
            )
          )
        )
      )
    )
  )
)
  )
)

(defun geoauto-adicionar-amostra-distancia
  (distancia amostra limite)
  (if (and (numberp distancia) (< (length amostra) limite))
    (append amostra (list distancia))
    amostra
  )
)

(defun geoauto-imprimir-amostra-distancias (amostra / distancia)
  (if amostra
    (progn
      (princ "\nAmostra de distâncias das curvas escolhidas (m):")
      (foreach distancia amostra
        (princ (strcat " " (rtos distancia 2 3)))
      )
    )
  )
)

(defun geoauto-juntar-strings (valores separador / resultado valor)
  (setq resultado "")
  (foreach valor valores
    (setq resultado
      (if (= resultado "")
        valor
        (strcat resultado separador valor)
      )
    )
  )
  resultado
)

(defun geoauto-adicionar-amostra-associacao
  (registro resultado amostra limite)
  (if (< (length amostra) limite)
    (append
      amostra
      (list
        (list
          (nth 7 registro)
          (nth 4 registro)
          (nth 1 registro)
          (nth 8 registro)
          (nth 33 resultado)
          (nth 29 resultado)
          (nth 22 resultado)
          (nth 31 resultado)
          (nth 32 resultado)
        )
      )
    )
    amostra
  )
)

(defun geoauto-imprimir-amostra-associacoes
  (titulo amostra / item codigo rede ponto layers status faixa
   primeira segunda layer-escolhido)
  (if (and *geoauto-debug* amostra)
    (progn
      (princ (strcat "\n" titulo))
      (foreach item amostra
        (setq codigo (nth 0 item))
        (setq rede (nth 1 item))
        (setq ponto (nth 2 item))
        (setq layers (nth 3 item))
        (setq status (nth 4 item))
        (setq faixa (nth 5 item))
        (setq primeira (nth 6 item))
        (setq segunda (nth 7 item))
        (setq layer-escolhido (nth 8 item))
        (princ
          (strcat
            "\n  Código: " (if codigo codigo "")
            " | Rede: " (if rede rede "")
            " | Ponto: X=" (rtos (car ponto) 2 3)
            " Y=" (rtos (cadr ponto) 2 3)
            " | Layers procurados: "
            (if layers (geoauto-juntar-strings layers ", ") "<nenhum>")
            " | Resultado: " (vl-princ-to-string status)
          )
        )
        (if faixa
          (princ
            (strcat " | Faixa: " (vl-princ-to-string faixa))
          )
        )
        (if (numberp primeira)
          (princ
            (strcat " | Primeira distância: " (rtos primeira 2 3) " m")
          )
        )
        (if (numberp segunda)
          (princ
            (strcat " | Segunda distância: " (rtos segunda 2 3) " m")
          )
        )
        (if layer-escolhido
          (princ (strcat " | Curva escolhida: " layer-escolhido))
        )
      )
    )
  )
)

(defun geoauto-adicionar-amostra-agua
  (registro resultado amostra limite)
  (if (< (length amostra) limite)
    (append
      amostra
      (list
        (list
          (nth 7 registro)          ; 0 codigo
          (nth 4 registro)          ; 1 rede
          (nth 1 registro)          ; 2 ponto CSV
          (nth 0 registro)          ; 3 layer de texto
          (nth 8 registro)          ; 4 layers procurados
          (nth 33 resultado)        ; 5 status associacao
          (nth 34 resultado)        ; 6 motivo fallback
          (nth 22 resultado)        ; 7 distancia permitida
          (nth 32 resultado)        ; 8 layer curva
          (nth 39 resultado)        ; 9 tipo curva
          (nth 40 resultado)        ; 10 handle curva
          (nth 29 resultado)        ; 11 faixa
          (nth 41 resultado)        ; 12 closest point
          (nth 36 resultado)        ; 13 parametro
          (nth 37 resultado)        ; 14 derivada
          (nth 38 resultado)        ; 15 amostragem
          (nth 35 resultado)        ; 16 tangente geral
          (nth 42 resultado)        ; 17 correspondencia layer
          (nth 51 resultado)        ; 18 rotacao final
          (nth 43 resultado)        ; 19 distancia global
          (nth 44 resultado)        ; 20 layer global
          (nth 45 resultado)        ; 21 tipo global
          (nth 49 resultado)        ; 22 tipo referencia
          (nth 50 resultado)        ; 23 nome referencia
          (nth 47 resultado)        ; 24 distancia fechada
          (nth 48 resultado)        ; 25 tipo nao suportado
        )
      )
    )
    amostra
  )
)

(defun geoauto-imprimir-amostra-agua
  (titulo amostra / item ponto layers)
  (if (and *geoauto-debug* amostra)
    (progn
      (princ (strcat "\n" titulo))
      (foreach item amostra
        (setq ponto (nth 2 item))
        (setq layers (nth 4 item))
        (princ
          (strcat
            "\n  Código normalizado: " (if (nth 0 item) (nth 0 item) "")
            " | Rede interpretada: " (if (nth 1 item) (nth 1 item) "")
            " | X=" (rtos (car ponto) 2 3)
            " Y=" (rtos (cadr ponto) 2 3)
            " | Layer de texto: " (if (nth 3 item) (nth 3 item) "")
            " | Layers procurados: "
            (if layers (geoauto-juntar-strings layers ", ") "<nenhum>")
          )
        )
        (princ
          (strcat
            "\n    Associação: " (vl-princ-to-string (nth 5 item))
            " | Motivo final: " (vl-princ-to-string (nth 6 item))
            " | Faixa: " (vl-princ-to-string (nth 11 item))
            " | Rotação final: "
            (if (numberp (nth 18 item)) (rtos (nth 18 item) 2 6) "<nil>")
          )
        )
        (princ
          (strcat
            "\n    Curva permitida: distância="
            (if (numberp (nth 7 item))
              (strcat (rtos (nth 7 item) 2 3) " m")
              "<não calculada>"
            )
            " | layer=" (if (nth 8 item) (nth 8 item) "<nenhum>")
            " | tipo=" (if (nth 9 item) (nth 9 item) "<nenhum>")
            " | handle=" (if (nth 10 item) (nth 10 item) "<nenhum>")
            " | correspondência=" (vl-princ-to-string (nth 17 item))
          )
        )
        (princ
          (strcat
            "\n    ClosestPoint=" (vl-princ-to-string (nth 12 item))
            " | parâmetro=" (vl-princ-to-string (nth 13 item))
            " | derivada=" (vl-princ-to-string (nth 14 item))
            " | amostragem=" (vl-princ-to-string (nth 15 item))
            " | tangente=" (vl-princ-to-string (nth 16 item))
          )
        )
        (if (numberp (nth 19 item))
          (princ
            (strcat
              "\n    Curva global próxima (somente diagnóstico): "
              (rtos (nth 19 item) 2 3) " m"
              " | layer=" (if (nth 20 item) (nth 20 item) "<nenhum>")
              " | tipo=" (if (nth 21 item) (nth 21 item) "<nenhum>")
            )
          )
        )
        (if (nth 22 item)
          (princ
            (strcat
              "\n    Referência próxima: "
              (vl-princ-to-string (nth 22 item))
              " | nome=" (if (nth 23 item) (nth 23 item) "<sem nome>")
            )
          )
        )
        (if (numberp (nth 24 item))
          (princ
            (strcat
              "\n    Curva fechada permitida mais próxima: "
              (rtos (nth 24 item) 2 3) " m"
            )
          )
        )
        (if (nth 25 item)
          (princ
            (strcat
              "\n    Entidade não suportada próxima: " (nth 25 item)
            )
          )
        )
      )
    )
  )
)

(defun geoauto-adicionar-amostra-orientacao
  (registro resultado amostra limite)
  (if (< (length amostra) limite)
    (append
      amostra
      (list
        (list
          (nth 7 registro)   ; 0 codigo
          (nth 4 registro)   ; 1 rede
          (nth 32 resultado) ; 2 layer curva
          (nth 39 resultado) ; 3 tipo curva
          (nth 40 resultado) ; 4 handle curva
          (nth 22 resultado) ; 5 distancia
          (nth 54 resultado) ; 6 angulo geometrico
          (nth 55 resultado) ; 7 angulo texto final
          (nth 52 resultado) ; 8 posicao inicial/base
          (nth 57 resultado) ; 9 posicao final
          (nth 58 resultado) ; 10 Rotation criada
          (nth 59 resultado) ; 11 motivo reposicionamento
          (nth 64 resultado) ; 12 igualdade confirmada
          (nth 53 resultado) ; 13 status contexto
        )
      )
    )
    amostra
  )
)

(defun geoauto-imprimir-amostra-orientacao
  (titulo amostra / item ponto-inicial ponto-final)
  (if (and *geoauto-debug* amostra)
    (progn
      (princ (strcat "\n" titulo))
      (foreach item amostra
        (setq ponto-inicial (nth 8 item))
        (setq ponto-final (nth 9 item))
        (princ
          (strcat
            "\n  Código: " (if (nth 0 item) (nth 0 item) "")
            " | Rede: " (if (nth 1 item) (nth 1 item) "")
            " | Curva escolhida: "
            (if (nth 2 item) (nth 2 item) "<sem layer>")
            "/"
            (if (nth 3 item) (nth 3 item) "<sem tipo>")
            "#"
            (if (nth 4 item) (nth 4 item) "<sem handle>")
            " | Distância: "
            (if (numberp (nth 5 item))
              (strcat (rtos (nth 5 item) 2 3) " m")
              "<nil>"
            )
          )
        )
        (princ
          (strcat
            "\n    Ângulo geométrico: "
            (if (numberp (nth 6 item))
              (rtos (nth 6 item) 2 8)
              "<nil>"
            )
            " | Ângulo final legível: "
            (if (numberp (nth 7 item))
              (rtos (nth 7 item) 2 8)
              "<nil>"
            )
            " | Rotação criada no TEXT: "
            (if (numberp (nth 10 item))
              (rtos (nth 10 item) 2 8)
              "<nil>"
            )
            " | angulo_texto_final = Rotation do objeto TEXT: "
            (if (> (nth 12 item) 0) "Sim" "Não")
          )
        )
        (princ
          (strcat
            "\n    Posição inicial: "
            (vl-princ-to-string ponto-inicial)
            " | Posição final: "
            (vl-princ-to-string ponto-final)
            " | Motivo do reposicionamento: "
            (vl-princ-to-string (nth 11 item))
            " | Status: "
            (vl-princ-to-string (nth 13 item))
          )
        )
      )
    )
  )
)

(defun geoauto-imprimir-tabela-curvas
  (titulo tabela layers-agua limite / item contador reconhecido)
  (if *geoauto-debug*
    (progn
      (princ (strcat "\n" titulo))
      (princ "\n  Layer encontrado | Tipo | Quantidade | Reconhecido como água?")
      (setq contador 0)
      (foreach item tabela
        (if (< contador limite)
          (progn
            (setq reconhecido
              (if (geoauto-layer-valido-p (nth 0 item) layers-agua)
                "Sim"
                "Não"
              )
            )
            (princ
              (strcat
                "\n  " (nth 0 item)
                " | " (nth 1 item)
                " | " (itoa (nth 2 item))
                " | " reconhecido
              )
            )
          )
        )
        (setq contador (1+ contador))
      )
      (if (> (length tabela) limite)
        (princ
          (strcat
            "\n  ... e mais " (itoa (- (length tabela) limite))
            " combinação(ões) de layer/tipo."
          )
        )
      )
      (if (null tabela) (princ "\n  <nenhuma>"))
    )
  )
)


(defun geoauto-quantidade-textos-registro (registro)
  (if (nth 3 registro) 2 1)
)

(defun geoauto-imprimir-layers (titulo layers limite / contador total)
  (princ (strcat "\n" titulo))
  (setq contador 0)
  (setq total (length layers))
  (if layers
    (progn
      (foreach layer layers
        (if (< contador limite)
          (princ (strcat "\n  - " layer))
        )
        (setq contador (1+ contador))
      )
      (if (> total limite)
        (princ
          (strcat
            "\n  - ... e mais "
            (itoa (- total limite))
            " layer(s)"
          )
        )
      )
    )
    (princ "\n  - nenhum")
  )
)

(defun geoauto-inserir-registro
  (registro curvas-por-layer curvas-abertas curvas-fechadas-por-layer
   entidades-nao-suportadas-por-layer referencias-diagnostico
   curvas-interferencia estilo altura afastamento
   raio-primario raio-secundario raio-maximo tolerancia-ambiguidade
   distancia-amostra passo-longitudinal max-deslocamento fator-margem
   passo-perpendicular max-afastamento-perpendicular
   fator-margem-curva verificar-interferencia habilitar-rotacao
   verificar-colisao-textos
   / layer ponto-csv texto-profundidade
     texto-diametro rede rede-pluvial pesquisa status ponto-base
     angulo tangente dados-angulo quantidade-registro inseridos
     rotacionados sem-entidade fora-raio rotacao-padrao
     erros-tangente erros-insercao associados-abertas fallbacks
     pluviais-rotacionados pluviais-sem-curva sem-entidade-textos
     fora-raio-textos modo-rotacionado resultado-grupo
     entidade-profundidade entidade-diametro deslocamento-grupo
     tentativas-reposicionamento colisoes-detectadas forcado-grupo
     grupos-processados grupos-posicao-original grupos-frente
     grupos-tras posicionamentos-forcados maior-deslocamento
     curvas-rejeitadas-distancia distancia-curva-escolhida
     afastamento-adicional tentativas-perpendiculares
     interferencias-curvas forcado-curva grupos-afastados-perpendicular
     maior-afastamento-adicional fallback-bbox layers-validos
     faixa-associacao associacao-ambigua segunda-distancia
     layer-curva-escolhida status-associacao dados-diagnostico
     motivo-fallback status-tangente status-parametro status-derivada
     status-amostragem tipo-curva handle-curva status-closest modo-layer
     distancia-global layer-global tipo-global handle-global
     distancia-fechada tipo-nao-suportado tipo-referencia nome-referencia
     rotacao-final contexto-orientacao contexto-orientacao-original
     status-contexto angulo-geometrico
     normal-contexto resultado-visual rotacao-texto-criada ponto-final
     motivo-posicionamento rotacao-preservada-fallback bbox-falhas-visuais
     falhas-anticolisao tentativas-rotacao-mantida rotacao-texto-confere
     fallback-visual-usado modo-ni destino-layer-ni layer-original
     contexto-ni-inferencia contexto-ni-proprio)
  (setq layer (nth 0 registro))
  (setq layer-original layer)
  (setq ponto-csv (nth 1 registro))
  (setq texto-profundidade (nth 2 registro))
  (setq texto-diametro (nth 3 registro))
  (setq rede (nth 4 registro))
  (setq modo-ni
    (=
      (geoauto-normalizar-layer rede)
      (geoauto-normalizar-layer *geoauto-rede-nao-identificada*)))
  (setq rede-pluvial (nth 5 registro))
  (setq *geoauto-registro-id-atual* (nth 6 registro))
  (setq *geoauto-codigo-atual* (nth 7 registro))
  (setq layers-validos (nth 8 registro))
  (setq *geoauto-ponto-original-atual* ponto-csv)
  (setq quantidade-registro (geoauto-quantidade-textos-registro registro))

  (setq inseridos 0)
  (setq rotacionados 0)
  (setq sem-entidade 0)
  (setq fora-raio 0)
  (setq rotacao-padrao 0)
  (setq erros-tangente 0)
  (setq erros-insercao 0)
  (setq associados-abertas 0)
  (setq fallbacks 0)
  (setq pluviais-rotacionados 0)
  (setq pluviais-sem-curva 0)
  (setq sem-entidade-textos 0)
  (setq fora-raio-textos 0)
  (setq modo-rotacionado nil)
  (setq ponto-base ponto-csv)
  (setq angulo nil)
  (setq tangente nil)
  (setq grupos-processados 1)
  (setq grupos-posicao-original 0)
  (setq grupos-frente 0)
  (setq grupos-tras 0)
  (setq tentativas-reposicionamento 0)
  (setq colisoes-detectadas 0)
  (setq posicionamentos-forcados 0)
  (setq maior-deslocamento 0.0)
  (setq curvas-rejeitadas-distancia 0)
  (setq distancia-curva-escolhida nil)
  (setq grupos-afastados-perpendicular 0)
  (setq tentativas-perpendiculares 0)
  (setq interferencias-curvas 0)
  (setq maior-afastamento-adicional 0.0)
  (setq forcado-curva 0)
  (setq fallback-bbox 0)
  (setq faixa-associacao nil)
  (setq associacao-ambigua nil)
  (setq segunda-distancia nil)
  (setq layer-curva-escolhida nil)
  (setq status-associacao 'NAO_EXECUTADO)
  (setq motivo-fallback nil)
  (setq status-tangente 'NAO_EXECUTADA)
  (setq status-parametro 'NAO_EXECUTADO)
  (setq status-derivada 'NAO_EXECUTADA)
  (setq status-amostragem 'NAO_NECESSARIA)
  (setq tipo-curva nil)
  (setq handle-curva nil)
  (setq status-closest 'NAO_EXECUTADO)
  (setq modo-layer nil)
  (setq distancia-global nil)
  (setq layer-global nil)
  (setq tipo-global nil)
  (setq handle-global nil)
  (setq distancia-fechada nil)
  (setq tipo-nao-suportado nil)
  (setq tipo-referencia nil)
  (setq nome-referencia nil)
  (setq rotacao-final nil)
  (setq contexto-orientacao nil)
  (setq contexto-orientacao-original nil)
  (setq contexto-ni-inferencia nil)
  (setq contexto-ni-proprio nil)
  (setq status-contexto 'SKIPPED_NO_ORIENTATION)
  (setq angulo-geometrico nil)
  (setq normal-contexto nil)
  (setq rotacao-texto-criada nil)
  (setq ponto-final ponto-csv)
  (setq motivo-posicionamento 'NAO_EXECUTADO)
  (setq rotacao-preservada-fallback 0)
  (setq bbox-falhas-visuais 0)
  (setq falhas-anticolisao 0)
  (setq tentativas-rotacao-mantida 0)
  (setq rotacao-texto-confere 0)
  (setq fallback-visual-usado 0)

  ; O resultado V2.1.7 resolve associação, âncora, tangente XY e ângulos
  ; em uma única estrutura. Nenhuma etapa posterior pode mutá-la.
  (setq contexto-orientacao
    (if habilitar-rotacao
      (geoauto-v214-resolver-orientacao
        ponto-csv curvas-por-layer layers-validos rede
        *geoauto-permitir-outra-rede* modo-ni)
      (geoauto-v214-criar-erro-orientacao
        'SKIPPED_NO_ORIENTATION rede ponto-csv 'ROTATION_DISABLED
        "A orientacao esta desativada; o TEXT usara contingencia explicita."
        0 nil)))
  ; Se a inferencia estrita nao for segura, preserva apenas a orientacao por
  ; geometria propria de NI. Outra rede nunca entra como fallback nesse caso.
  (if
    (and
      habilitar-rotacao modo-ni
      (not (eq
        (geoauto-v214-valor contexto-orientacao 'STATUS)
        'ORIENTATION_OK)))
    (progn
      (setq contexto-ni-inferencia contexto-orientacao)
      (setq contexto-ni-proprio
        (geoauto-v214-resolver-orientacao
          ponto-csv curvas-por-layer layers-validos rede nil nil))
      (if
        (eq
          (geoauto-v214-valor contexto-ni-proprio 'STATUS)
          'ORIENTATION_OK)
        (setq contexto-orientacao contexto-ni-proprio)
        (setq contexto-orientacao contexto-ni-inferencia))))
  (setq contexto-orientacao-original contexto-orientacao)
  (setq status-contexto
    (geoauto-v214-valor contexto-orientacao-original 'STATUS)
  )
  (setq modo-rotacionado (eq status-contexto 'ORIENTATION_OK))
  (setq destino-layer-ni
    (geoauto-v214-valor
      contexto-orientacao-original 'DESTINATION_TEXT_LAYER))
  (if (and modo-ni modo-rotacionado destino-layer-ni)
    (progn
      (setq layer destino-layer-ni)
      (geoauto-debug
        (strcat
          "NI associada: " layer-original " -> " layer
          " | curva: "
          (geoauto-v214-valor contexto-orientacao-original 'SOURCE_LAYER)
          " | distancia: "
          (rtos
            (geoauto-v214-valor contexto-orientacao-original 'DISTANCE_2D)
            2 3)
          " m"))))
  (if (not modo-rotacionado)
    (setq contexto-orientacao
      (geoauto-v215-criar-contingencia-horizontal
        contexto-orientacao-original)))
  (setq *geoauto-contexto-orientacao-atual* contexto-orientacao)
  (setq ponto-base
    (geoauto-v214-valor contexto-orientacao 'PONTO_BASE)
  )
  (setq angulo
    (geoauto-v214-valor contexto-orientacao 'READABLE_ANGLE_RAD)
  )
  (setq rotacao-final angulo)
  (setq angulo-geometrico
    (geoauto-v214-valor contexto-orientacao 'GEOMETRIC_ANGLE_RAD)
  )
  (setq tangente
    (geoauto-v214-valor contexto-orientacao 'READABLE_TANGENT_XY)
  )
  (setq normal-contexto
    (geoauto-v214-valor contexto-orientacao 'NORMAL_XY)
  )
  (setq status (if modo-rotacionado 'OK status-contexto))
  (setq status-associacao status)
  (setq motivo-fallback (geoauto-v214-valor contexto-orientacao 'ERROR_CODE))
  (setq distancia-curva-escolhida
    (geoauto-v214-valor contexto-orientacao 'DISTANCE_2D))
  (setq layer-curva-escolhida
    (geoauto-v214-valor contexto-orientacao 'SOURCE_LAYER))
  (setq tipo-curva (geoauto-v214-valor contexto-orientacao 'SOURCE_TYPE))
  (setq handle-curva (geoauto-v214-valor contexto-orientacao 'SOURCE_HANDLE))
  (setq status-closest (if modo-rotacionado 'OK_CLOSEST_POINT 'NAO_RESOLVIDO))
  (setq modo-layer
    (if (geoauto-v214-valor contexto-orientacao 'CROSS_NETWORK)
      'OUTRA_REDE_TUBULACAO
      'REDE_PRINCIPAL))
  (setq curvas-rejeitadas-distancia
    (length (geoauto-v214-valor contexto-orientacao 'CANDIDATES_REJECTED)))
  (setq faixa-associacao
    (cond
      ((equal (geoauto-v214-valor contexto-orientacao 'SEARCH_RADIUS)
              raio-primario 1.0e-12) 'PRIMARIO)
      ((equal (geoauto-v214-valor contexto-orientacao 'SEARCH_RADIUS)
              raio-secundario 1.0e-12) 'SECUNDARIO)
      ((equal (geoauto-v214-valor contexto-orientacao 'SEARCH_RADIUS)
              raio-maximo 1.0e-12) 'MAXIMO)
      (T nil)))
  (setq status-tangente
    (if modo-rotacionado
      (geoauto-v214-valor contexto-orientacao 'TANGENT_METHOD)
      'REJECTED_INVALID_TANGENT_XY))
  (setq status-parametro
    (if (numberp (geoauto-v214-valor contexto-orientacao 'CURVE_PARAMETER))
      'OK_PARAMETRO 'ERRO_PARAMETRO_CURVA))
  (setq status-derivada 'SUBSISTEMA_V214)
  (setq status-amostragem status-tangente)
  (if modo-rotacionado
    (setq associados-abertas quantidade-registro)
    (progn
      (setq sem-entidade 1)
      (setq sem-entidade-textos quantidade-registro)
      (if (eq motivo-fallback 'REJECTED_INVALID_TANGENT_XY)
        (setq erros-tangente 1))))

  (if (and (not modo-rotacionado) (null motivo-fallback))
    (progn
      (setq dados-diagnostico
        (geoauto-classificar-motivo-fallback
          ponto-csv status layers-validos raio-maximo curvas-abertas
          curvas-fechadas-por-layer entidades-nao-suportadas-por-layer
          referencias-diagnostico
        )
      )
      (setq motivo-fallback (nth 0 dados-diagnostico))
      (setq distancia-global (nth 1 dados-diagnostico))
      (setq layer-global (nth 2 dados-diagnostico))
      (setq tipo-global (nth 3 dados-diagnostico))
      (setq handle-global (nth 4 dados-diagnostico))
      (setq distancia-fechada (nth 5 dados-diagnostico))
      (setq tipo-nao-suportado (nth 6 dados-diagnostico))
      (setq tipo-referencia (nth 7 dados-diagnostico))
      (setq nome-referencia (nth 8 dados-diagnostico))
    )
  )

  (if (not modo-rotacionado)
    (progn
      (setq fallbacks quantidade-registro)
      (if rede-pluvial
        (setq pluviais-sem-curva quantidade-registro)
      )
    )
  )

  ; ETAPAS 5, 6 e 7: posição-base, afastamento perpendicular e anticolisão
  ; recebem o contexto pronto. Nenhuma função visual recalcula a orientação.
  (if (not modo-rotacionado)
    (setq resultado-grupo
      (geoauto-inserir-contexto-fallback-visual
        layer estilo contexto-orientacao afastamento altura
        texto-profundidade texto-diametro 0.0 0.0
        'CONTINGENCIA_HORIZONTAL_SEM_ORIENTACAO)
    )
    (progn
      (setq resultado-visual
        (vl-catch-all-apply
          'geoauto-inserir-grupo-textos-contexto
          (list
            layer estilo contexto-orientacao afastamento altura
            texto-profundidade texto-diametro passo-longitudinal
            max-deslocamento fator-margem curvas-interferencia
            passo-perpendicular max-afastamento-perpendicular
            fator-margem-curva verificar-interferencia verificar-colisao-textos
          )
        )
      )
      (if (vl-catch-all-error-p resultado-visual)
        (setq resultado-grupo
          (geoauto-inserir-contexto-fallback-visual
            layer estilo contexto-orientacao afastamento altura
            texto-profundidade texto-diametro 0.0 0.0
            'ERRO_VISUAL_ROTACAO_PRESERVADA
          )
        )
        (setq resultado-grupo resultado-visual)
      )
    )
  )

  (setq tentativas-reposicionamento (nth 4 resultado-grupo))
  (setq colisoes-detectadas (nth 5 resultado-grupo))
  (setq forcado-grupo (nth 6 resultado-grupo))
  (setq afastamento-adicional (nth 8 resultado-grupo))
  (setq tentativas-perpendiculares (nth 9 resultado-grupo))
  (setq interferencias-curvas (nth 10 resultado-grupo))
  (if (nth 11 resultado-grupo) (setq forcado-curva 1))
  (if (nth 12 resultado-grupo) (setq fallback-bbox 1))
  (if (nth 13 resultado-grupo) (setq fallback-visual-usado 1))
  (setq falhas-anticolisao
    (if (numberp (nth 14 resultado-grupo))
      (nth 14 resultado-grupo)
      0
    )
  )
  (setq rotacao-texto-criada (nth 15 resultado-grupo))
  (setq ponto-final
    (if (nth 16 resultado-grupo)
      (nth 16 resultado-grupo)
      ponto-base
    )
  )
  (setq motivo-posicionamento
    (if (nth 17 resultado-grupo)
      (nth 17 resultado-grupo)
      'NAO_INFORMADO
    )
  )
  (if (nth 18 resultado-grupo)
    (setq rotacao-preservada-fallback 1)
  )
  (setq bbox-falhas-visuais
    (if (numberp (nth 19 resultado-grupo))
      (nth 19 resultado-grupo)
      fallback-bbox
    )
  )
  (setq tentativas-rotacao-mantida
    (if (numberp (nth 20 resultado-grupo))
      (nth 20 resultado-grupo)
      0
    )
  )
  (if (nth 21 resultado-grupo)
    (setq rotacao-texto-confere 1)
  )

  (if (eq (nth 0 resultado-grupo) 'OK)
    (progn
      (setq entidade-profundidade (nth 1 resultado-grupo))
      (setq entidade-diametro (nth 2 resultado-grupo))
      (setq deslocamento-grupo (nth 3 resultado-grupo))
      (if entidade-profundidade (setq inseridos (1+ inseridos)))
      (if entidade-diametro (setq inseridos (1+ inseridos)))

      (if modo-rotacionado
        (progn
          (setq rotacionados inseridos)
          (if rede-pluvial
            (setq pluviais-rotacionados inseridos)
          )
        )
        (setq rotacao-padrao inseridos)
      )

      (cond
        ((equal deslocamento-grupo 0.0 1.0e-12)
          (setq grupos-posicao-original 1)
        )
        ((> deslocamento-grupo 0.0)
          (setq grupos-frente 1)
        )
        (T
          (setq grupos-tras 1)
        )
      )
      (if (> afastamento-adicional 0.0)
        (setq grupos-afastados-perpendicular 1)
      )
      (if forcado-grupo
        (setq posicionamentos-forcados 1)
      )
      (setq maior-deslocamento (abs deslocamento-grupo))
      (setq maior-afastamento-adicional afastamento-adicional)
      (setq erros-insercao (- quantidade-registro inseridos))
    )
    (setq erros-insercao quantidade-registro)
  )

  (geoauto-v214-registrar-diagnostico-final
    contexto-orientacao
    *geoauto-validacoes-texto-atual*
    (geoauto-v214-status-final
      contexto-orientacao *geoauto-validacoes-texto-atual*
      inseridos quantidade-registro))

  (list
    inseridos
    rotacionados
    sem-entidade
    fora-raio
    rotacao-padrao
    erros-tangente
    erros-insercao
    associados-abertas
    fallbacks
    pluviais-rotacionados
    pluviais-sem-curva
    sem-entidade-textos
    fora-raio-textos
    grupos-processados
    grupos-posicao-original
    grupos-frente
    grupos-tras
    tentativas-reposicionamento
    colisoes-detectadas
    posicionamentos-forcados
    maior-deslocamento
    curvas-rejeitadas-distancia
    distancia-curva-escolhida
    grupos-afastados-perpendicular
    tentativas-perpendiculares
    interferencias-curvas
    maior-afastamento-adicional
    forcado-curva
    fallback-bbox
    faixa-associacao
    (if associacao-ambigua 1 0)
    segunda-distancia
    layer-curva-escolhida
    status-associacao
    motivo-fallback
    status-tangente
    status-parametro
    status-derivada
    status-amostragem
    tipo-curva
    handle-curva
    status-closest
    modo-layer
    distancia-global
    layer-global
    tipo-global
    handle-global
    distancia-fechada
    tipo-nao-suportado
    tipo-referencia
    nome-referencia
    rotacao-final
    ponto-base
    status-contexto
    angulo-geometrico
    rotacao-final
    normal-contexto
    ponto-final
    rotacao-texto-criada
    motivo-posicionamento
    rotacao-preservada-fallback
    bbox-falhas-visuais
    falhas-anticolisao
    tentativas-rotacao-mantida
    rotacao-texto-confere
    fallback-visual-usado
  )
)


;;; GEOAUTOENGINE ORIENTATION RUNTIME V2.1.7
;;; Contrato: WCS, metros, plano XY e TEXT com normal WCS Z.
(setq *geoauto-orientation-runtime-version* "2.1.7")
(setq *geoauto-diagnosticos-orientacao* nil)
(setq *geoauto-validacoes-texto-atual* nil)
(setq *geoauto-space-atual* nil)

(defun geoauto-v214-valor (resultado chave / item)
  (setq item (if resultado (assoc chave resultado) nil))
  (if item (cdr item) nil)
)

(defun geoauto-contexto-rotacao-resolvida-p (contexto)
  (if
    (member
      (geoauto-v214-valor contexto 'STATUS)
      '(ORIENTATION_OK ORIENTATION_FALLBACK))
    T
    nil)
)

(defun geoauto-normalizar-angulo-cad (angulo / dois-pi)
  ; Valor ausente ou inválido continua inválido. Nunca vira zero.
  (if (not (numberp angulo))
    nil
    (progn
      (setq dois-pi (* 2.0 pi))
      (while (< angulo 0.0) (setq angulo (+ angulo dois-pi)))
      (while (>= angulo dois-pi) (setq angulo (- angulo dois-pi)))
      angulo
    )
  )
)

(defun geoauto-diferenca-angular (angulo-a angulo-b / a b diferenca dois-pi)
  (setq a (geoauto-normalizar-angulo-cad angulo-a))
  (setq b (geoauto-normalizar-angulo-cad angulo-b))
  (if (and (numberp a) (numberp b))
    (progn
      (setq dois-pi (* 2.0 pi))
      (setq diferenca (abs (- a b)))
      (min diferenca (- dois-pi diferenca))
    )
    nil
  )
)

(defun geoauto-v214-normalizar-vetor-xy (vetor / dx dy modulo)
  ; A projeção XY ocorre antes da normalização. Z nunca participa.
  (if (and (listp vetor) (numberp (car vetor)) (numberp (cadr vetor)))
    (progn
      (setq dx (float (car vetor)))
      (setq dy (float (cadr vetor)))
      (setq modulo (sqrt (+ (* dx dx) (* dy dy))))
      (if (>= modulo *geoauto-tolerancia-tangente-xy*)
        (list (/ dx modulo) (/ dy modulo) 0.0)
        nil
      )
    )
    nil
  )
)

(defun geoauto-v215-ponto-mais-proximo-xy
  (entidade ponto-wcs / projetado direto metodo)
  ; A associação é planimétrica. O ponto topográfico chega com Z=0, enquanto
  ; a tubulação pode estar na cota real. A projeção ao longo do eixo Z precisa
  ; acontecer antes da escolha do ponto da curva; descartar Z apenas depois de
  ; getClosestPointTo pode selecionar o trecho errado pela distância 3D.
  (setq projetado
    (geoauto-chamada-segura
      'vlax-curve-getClosestPointToProjection
      (list entidade ponto-wcs '(0.0 0.0 1.0))))
  (if (and projetado (listp projetado))
    (list projetado 'WCS_XY_PROJECTION)
    (progn
      ; Compatibilidade controlada para tipos/versões que não exponham a API
      ; de projeção. O método fica explícito no diagnóstico.
      (setq direto
        (geoauto-chamada-segura
          'vlax-curve-getClosestPointTo (list entidade ponto-wcs)))
      (if (and direto (listp direto))
        (list direto 'CLOSEST_3D_COMPATIBILITY)
        (list nil 'CLOSEST_POINT_FAILED)
      )
    )
  )
)

(defun geoauto-v215-distancia-3d (ponto-a ponto-b)
  (if (and (listp ponto-a) (listp ponto-b))
    (distance ponto-a ponto-b)
    nil
  )
)

(defun geoauto-v214-angulo-legivel (angulo / legivel invertido)
  (if (not (numberp angulo))
    nil
    (progn
      (setq legivel (atan (sin angulo) (cos angulo)))
      (setq invertido nil)
      (if (or (> legivel (/ pi 2.0)) (<= legivel (- (/ pi 2.0))))
        (progn
          (setq legivel (if (> legivel 0.0) (- legivel pi) (+ legivel pi)))
          (setq invertido T)
        )
      )
      (list (atan (sin legivel) (cos legivel)) invertido)
    )
  )
)

(defun geoauto-v214-parametro-e-distancia
  (entidade ponto / parametro distancia)
  (setq parametro
    (geoauto-chamada-segura 'vlax-curve-getParamAtPoint (list entidade ponto))
  )
  (setq distancia
    (geoauto-chamada-segura 'vlax-curve-getDistAtPoint (list entidade ponto))
  )
  (if (and (not (numberp parametro)) (numberp distancia))
    (setq parametro
      (geoauto-chamada-segura 'vlax-curve-getParamAtDist (list entidade distancia))
    )
  )
  (if (and (not (numberp distancia)) (numberp parametro))
    (setq distancia
      (geoauto-chamada-segura 'vlax-curve-getDistAtParam (list entidade parametro))
    )
  )
  (list parametro distancia)
)

(defun geoauto-v214-parametro-proximo-vertice-p
  (entidade parametro ponto / nome inicio fim inteiro ponto-vertice)
  (setq nome (geoauto-chamada-segura 'vla-get-ObjectName (list entidade)))
  (setq inicio (geoauto-chamada-segura 'vlax-curve-getStartParam (list entidade)))
  (setq fim (geoauto-chamada-segura 'vlax-curve-getEndParam (list entidade)))
  (setq inteiro (if (numberp parametro) (fix (+ parametro 0.5)) nil))
  (setq ponto-vertice
    (if (numberp inteiro)
      (geoauto-chamada-segura
        'vlax-curve-getPointAtParam (list entidade inteiro))
      nil))
  (if
    (and
      (member nome '("AcDbPolyline" "AcDb2dPolyline" "AcDb3dPolyline"))
      (numberp parametro) (numberp inicio) (numberp fim) (numberp inteiro)
      (> inteiro inicio) (< inteiro fim)
      (or
        (<= (abs (- parametro inteiro)) *geoauto-tolerancia-parametro-vertice*)
        (and ponto ponto-vertice
          (<= (geoauto-distancia-2d ponto ponto-vertice)
              *geoauto-tolerancia-espacial-vertice*)))
    )
    T
    nil
  )
)

(defun geoauto-v214-criar-tentativa (metodo sucesso vetor motivo)
  (list
    (cons 'METHOD metodo)
    (cons 'SUCCESS sucesso)
    (cons 'TANGENT_XY vetor)
    (cons 'FAILURE_REASON motivo)
  )
)

(defun geoauto-v214-tangente-amostragem-distancia
  (entidade ponto distancia-curva proximo-vertice
   / parametro-final distancia-final delta antes depois vetor metodo)
  (setq parametro-final
    (geoauto-chamada-segura 'vlax-curve-getEndParam (list entidade))
  )
  (setq distancia-final
    (if (numberp parametro-final)
      (geoauto-chamada-segura
        'vlax-curve-getDistAtParam (list entidade parametro-final))
      nil
    )
  )
  (if (and (numberp distancia-curva) (numberp distancia-final)
           (> distancia-final *geoauto-tolerancia-tangente-xy*))
    (progn
      (setq delta
        (min *geoauto-amostra-tangente* (max 1.0e-6 (/ distancia-final 1000.0)))
      )
      (setq antes
        (if (> distancia-curva 0.0)
          (geoauto-chamada-segura
            'vlax-curve-getPointAtDist
            (list entidade (max 0.0 (- distancia-curva delta))))
          nil
        )
      )
      (setq depois
        (if (< distancia-curva distancia-final)
          (geoauto-chamada-segura
            'vlax-curve-getPointAtDist
            (list entidade (min distancia-final (+ distancia-curva delta))))
          nil
        )
      )
      (setq vetor
        (cond
          ((and antes depois) (mapcar '- depois antes))
          (depois (mapcar '- depois ponto))
          (antes (mapcar '- ponto antes))
          (T nil)
        )
      )
      (setq vetor (geoauto-v214-normalizar-vetor-xy vetor))
      (setq metodo
        (cond
          (proximo-vertice 'VERTEX_DISTANCE_SAMPLE)
          ((and antes depois) 'DISTANCE_SAMPLE_CENTRAL)
          (depois 'DISTANCE_SAMPLE_FORWARD)
          (antes 'DISTANCE_SAMPLE_BACKWARD)
          (T 'DISTANCE_SAMPLE)
        )
      )
      (geoauto-v214-criar-tentativa
        metodo (if vetor T nil) vetor
        (if vetor nil 'REJECTED_INVALID_TANGENT_XY)
      )
    )
    (geoauto-v214-criar-tentativa
      'DISTANCE_SAMPLE nil nil 'CURVE_DISTANCE_UNAVAILABLE
    )
  )
)

(defun geoauto-v214-tangente-vertice
  (entidade ponto-csv parametro
   / inicio fim delta anterior vertice posterior proj-anterior proj-posterior
     distancia-anterior distancia-posterior vetor metodo)
  (setq inicio (geoauto-chamada-segura 'vlax-curve-getStartParam (list entidade)))
  (setq fim (geoauto-chamada-segura 'vlax-curve-getEndParam (list entidade)))
  (setq delta 0.25)
  (if (and (numberp inicio) (numberp fim) (numberp parametro))
    (progn
      (setq vertice
        (geoauto-chamada-segura 'vlax-curve-getPointAtParam (list entidade parametro))
      )
      (setq anterior
        (geoauto-chamada-segura
          'vlax-curve-getPointAtParam (list entidade (max inicio (- parametro delta)))
        )
      )
      (setq posterior
        (geoauto-chamada-segura
          'vlax-curve-getPointAtParam (list entidade (min fim (+ parametro delta)))
        )
      )
      (if (and anterior vertice posterior)
        (progn
          (setq proj-anterior
            (geoauto-ponto-segmento-mais-proximo-2d ponto-csv anterior vertice)
          )
          (setq proj-posterior
            (geoauto-ponto-segmento-mais-proximo-2d ponto-csv vertice posterior)
          )
          (setq distancia-anterior (geoauto-distancia-2d ponto-csv proj-anterior))
          (setq distancia-posterior (geoauto-distancia-2d ponto-csv proj-posterior))
          (cond
            ((< (+ distancia-anterior 1.0e-9) distancia-posterior)
              (setq vetor (mapcar '- vertice anterior))
              (setq metodo 'VERTEX_PREVIOUS_SEGMENT)
            )
            ((< (+ distancia-posterior 1.0e-9) distancia-anterior)
              (setq vetor (mapcar '- posterior vertice))
              (setq metodo 'VERTEX_NEXT_SEGMENT)
            )
            (T
              (setq vetor (mapcar '- posterior anterior))
              (setq metodo 'VERTEX_CENTRAL_CHORD)
            )
          )
          (setq vetor (geoauto-v214-normalizar-vetor-xy vetor))
          (geoauto-v214-criar-tentativa
            metodo (if vetor T nil) vetor
            (if vetor nil 'REJECTED_INVALID_TANGENT_XY)
          )
        )
        (geoauto-v214-criar-tentativa
          'VERTEX_POLICY nil nil 'VERTEX_SAMPLE_POINTS_MISSING
        )
      )
    )
    (geoauto-v214-criar-tentativa
      'VERTEX_POLICY nil nil 'VERTEX_PARAMETER_UNAVAILABLE
    )
  )
)

(defun geoauto-v214-tangente-derivada
  (entidade parametro / vetor)
  (setq vetor
    (if (numberp parametro)
      (geoauto-chamada-segura 'vlax-curve-getFirstDeriv (list entidade parametro))
      nil
    )
  )
  (setq vetor (geoauto-v214-normalizar-vetor-xy vetor))
  (geoauto-v214-criar-tentativa
    'NATIVE_DERIVATIVE (if vetor T nil) vetor
    (if vetor nil 'REJECTED_INVALID_TANGENT_XY)
  )
)

(defun geoauto-v214-tangente-segmento-local
  (entidade parametro / nome inicio fim indice p1 p2 vetor)
  (setq nome (geoauto-chamada-segura 'vla-get-ObjectName (list entidade)))
  (setq inicio (geoauto-chamada-segura 'vlax-curve-getStartParam (list entidade)))
  (setq fim (geoauto-chamada-segura 'vlax-curve-getEndParam (list entidade)))
  (if
    (and
      (member nome '("AcDbPolyline" "AcDb2dPolyline" "AcDb3dPolyline"))
      (numberp parametro) (numberp inicio) (numberp fim) (> fim inicio))
    (progn
      (setq indice (fix parametro))
      (if (>= indice fim) (setq indice (max inicio (1- (fix fim)))))
      (setq p1
        (geoauto-chamada-segura
          'vlax-curve-getPointAtParam (list entidade indice)))
      (setq p2
        (geoauto-chamada-segura
          'vlax-curve-getPointAtParam (list entidade (min fim (1+ indice)))))
      (setq vetor
        (if (and p1 p2)
          (geoauto-v214-normalizar-vetor-xy (mapcar '- p2 p1))
          nil))
      (geoauto-v214-criar-tentativa
        'LOCAL_SEGMENT (if vetor T nil) vetor
        (if vetor nil 'REJECTED_INVALID_TANGENT_XY))
    )
    (geoauto-v214-criar-tentativa
      'LOCAL_SEGMENT nil nil 'LOCAL_SEGMENT_UNAVAILABLE)
  )
)

(defun geoauto-v214-tangente-amostragem-parametro
  (entidade ponto parametro / inicio fim delta antes depois vetor)
  (setq inicio (geoauto-chamada-segura 'vlax-curve-getStartParam (list entidade)))
  (setq fim (geoauto-chamada-segura 'vlax-curve-getEndParam (list entidade)))
  (if (and (numberp parametro) (numberp inicio) (numberp fim) (> fim inicio))
    (progn
      (setq delta (min 0.01 (max 1.0e-6 (/ (- fim inicio) 1000.0))))
      (setq antes
        (if (> parametro inicio)
          (geoauto-chamada-segura
            'vlax-curve-getPointAtParam (list entidade (max inicio (- parametro delta))))
          nil
        )
      )
      (setq depois
        (if (< parametro fim)
          (geoauto-chamada-segura
            'vlax-curve-getPointAtParam (list entidade (min fim (+ parametro delta))))
          nil
        )
      )
      (setq vetor
        (cond
          ((and antes depois) (mapcar '- depois antes))
          (depois (mapcar '- depois ponto))
          (antes (mapcar '- ponto antes))
          (T nil)
        )
      )
      (setq vetor (geoauto-v214-normalizar-vetor-xy vetor))
      (geoauto-v214-criar-tentativa
        'PARAMETER_SAMPLE (if vetor T nil) vetor
        (if vetor nil 'REJECTED_INVALID_TANGENT_XY)
      )
    )
    (geoauto-v214-criar-tentativa
      'PARAMETER_SAMPLE nil nil 'CURVE_PARAMETER_UNAVAILABLE
    )
  )
)

(defun geoauto-v214-tangente-projecao-diferencial
  (entidade ponto / e p1 p2 p3 p4 q1 q2 q3 q4 vx vy lx ly vetor)
  (setq e (max 1.0e-4 (min 0.05 *geoauto-amostra-tangente*)))
  (setq p1 (list (- (car ponto) e) (cadr ponto) (if (caddr ponto) (caddr ponto) 0.0)))
  (setq p2 (list (+ (car ponto) e) (cadr ponto) (if (caddr ponto) (caddr ponto) 0.0)))
  (setq p3 (list (car ponto) (- (cadr ponto) e) (if (caddr ponto) (caddr ponto) 0.0)))
  (setq p4 (list (car ponto) (+ (cadr ponto) e) (if (caddr ponto) (caddr ponto) 0.0)))
  (setq q1 (geoauto-chamada-segura 'vlax-curve-getClosestPointTo (list entidade p1)))
  (setq q2 (geoauto-chamada-segura 'vlax-curve-getClosestPointTo (list entidade p2)))
  (setq q3 (geoauto-chamada-segura 'vlax-curve-getClosestPointTo (list entidade p3)))
  (setq q4 (geoauto-chamada-segura 'vlax-curve-getClosestPointTo (list entidade p4)))
  (setq vx (if (and q1 q2) (mapcar '- q2 q1) nil))
  (setq vy (if (and q3 q4) (mapcar '- q4 q3) nil))
  (setq lx (if vx (sqrt (+ (* (car vx) (car vx)) (* (cadr vx) (cadr vx)))) 0.0))
  (setq ly (if vy (sqrt (+ (* (car vy) (car vy)) (* (cadr vy) (cadr vy)))) 0.0))
  (setq vetor (geoauto-v214-normalizar-vetor-xy (if (>= lx ly) vx vy)))
  (geoauto-v214-criar-tentativa
    'DIFFERENTIAL_PROJECTION (if vetor T nil) vetor
    (if vetor nil 'REJECTED_INVALID_TANGENT_XY)
  )
)

(defun geoauto-v214-resolver-tangente
  (entidade ponto-csv ponto-proximo
   / param-dist parametro distancia-curva perto-vertice tentativas tentativa
     vetor metodo)
  (setq param-dist (geoauto-v214-parametro-e-distancia entidade ponto-proximo))
  (setq parametro (nth 0 param-dist))
  (setq distancia-curva (nth 1 param-dist))
  (setq perto-vertice
    (geoauto-v214-parametro-proximo-vertice-p
      entidade parametro ponto-proximo)
  )
  (setq tentativas nil)
  (setq vetor nil)
  (setq metodo nil)

  ; Em vértice, a decisão usa amostras dos dois trechos e o ponto CSV.
  (if perto-vertice
    (progn
      (setq tentativa
        (geoauto-v214-tangente-vertice entidade ponto-csv parametro)
      )
      (setq tentativas (append tentativas (list tentativa)))
      (if (geoauto-v214-valor tentativa 'SUCCESS)
        (progn
          (setq vetor (geoauto-v214-valor tentativa 'TANGENT_XY))
          (setq metodo (geoauto-v214-valor tentativa 'METHOD))
        )
      )
    )
  )

  ; Para pontos comuns, amostragem por comprimento é a primeira estratégia.
  (if (null vetor)
    (progn
      (setq tentativa
        (geoauto-v214-tangente-amostragem-distancia
          entidade ponto-proximo distancia-curva perto-vertice)
      )
      (setq tentativas (append tentativas (list tentativa)))
      (if (geoauto-v214-valor tentativa 'SUCCESS)
        (progn
          (setq vetor (geoauto-v214-valor tentativa 'TANGENT_XY))
          (setq metodo (geoauto-v214-valor tentativa 'METHOD))
        )
      )
    )
  )

  ; A derivada nativa nunca é aceita no parâmetro exato de um vértice.
  (if (and (null vetor) (not perto-vertice))
    (progn
      (setq tentativa (geoauto-v214-tangente-derivada entidade parametro))
      (setq tentativas (append tentativas (list tentativa)))
      (if (geoauto-v214-valor tentativa 'SUCCESS)
        (progn
          (setq vetor (geoauto-v214-valor tentativa 'TANGENT_XY))
          (setq metodo (geoauto-v214-valor tentativa 'METHOD))
        )
      )
    )
  )

  (if (null vetor)
    (progn
      (setq tentativa
        (geoauto-v214-tangente-segmento-local entidade parametro)
      )
      (setq tentativas (append tentativas (list tentativa)))
      (if (geoauto-v214-valor tentativa 'SUCCESS)
        (progn
          (setq vetor (geoauto-v214-valor tentativa 'TANGENT_XY))
          (setq metodo (geoauto-v214-valor tentativa 'METHOD))
        )
      )
    )
  )

  (if (null vetor)
    (progn
      (setq tentativa
        (geoauto-v214-tangente-amostragem-parametro
          entidade ponto-proximo parametro)
      )
      (setq tentativas (append tentativas (list tentativa)))
      (if (geoauto-v214-valor tentativa 'SUCCESS)
        (progn
          (setq vetor (geoauto-v214-valor tentativa 'TANGENT_XY))
          (setq metodo (geoauto-v214-valor tentativa 'METHOD))
        )
      )
    )
  )

  (if (null vetor)
    (progn
      (setq tentativa
        (geoauto-v214-tangente-projecao-diferencial entidade ponto-proximo)
      )
      (setq tentativas (append tentativas (list tentativa)))
      (if (geoauto-v214-valor tentativa 'SUCCESS)
        (progn
          (setq vetor (geoauto-v214-valor tentativa 'TANGENT_XY))
          (setq metodo (geoauto-v214-valor tentativa 'METHOD))
        )
      )
    )
  )

  (list
    (cons 'SUCCESS (if vetor T nil))
    (cons 'TANGENT_XY vetor)
    (cons 'TANGENT_METHOD metodo)
    (cons 'CURVE_PARAMETER parametro)
    (cons 'CURVE_DISTANCE distancia-curva)
    (cons 'NEAR_VERTEX perto-vertice)
    (cons 'ATTEMPTS tentativas)
    (cons 'ERROR_CODE (if vetor nil 'REJECTED_INVALID_TANGENT_XY))
  )
)

(defun geoauto-v214-avaliar-entidade
  (entidade ponto-csv layers-rede raio-maximo
   / resultado-projecao ponto-proximo metodo-projecao distancia distancia-3d
     layer handle tipo correspondencia tangente)
  (setq resultado-projecao
    (geoauto-v215-ponto-mais-proximo-xy entidade ponto-csv))
  (setq ponto-proximo (nth 0 resultado-projecao))
  (setq metodo-projecao (nth 1 resultado-projecao))
  (setq layer (geoauto-chamada-segura 'vla-get-Layer (list entidade)))
  (setq handle (geoauto-handle-entidade entidade))
  (setq tipo (geoauto-chamada-segura 'vla-get-ObjectName (list entidade)))
  (setq correspondencia
    (if layer (geoauto-modo-correspondencia-layer layer layers-rede) nil)
  )
  (if (and ponto-proximo (listp ponto-proximo))
    (progn
      (setq distancia (geoauto-distancia-2d ponto-csv ponto-proximo))
      (setq distancia-3d (geoauto-v215-distancia-3d ponto-csv ponto-proximo))
      (setq tangente
        (if (<= distancia raio-maximo)
          (geoauto-v214-resolver-tangente entidade ponto-csv ponto-proximo)
          nil))
      (list
        (cons 'ENTITY entidade)
        (cons 'HANDLE handle)
        (cons 'TYPE tipo)
        (cons 'LAYER layer)
        (cons 'NETWORK_MATCH (if correspondencia T nil))
        (cons 'LAYER_MATCH_MODE correspondencia)
        (cons 'CLOSEST_POINT_WCS ponto-proximo)
        (cons 'DISTANCE_2D distancia)
        (cons 'DISTANCE_3D distancia-3d)
        (cons 'CLOSEST_POINT_METHOD metodo-projecao)
        (cons 'TANGENT_RESULT tangente)
        (cons 'VALID
          (if tangente (geoauto-v214-valor tangente 'SUCCESS) nil))
        (cons 'REJECTION_REASON
          (cond
            ((> distancia raio-maximo) 'OUTSIDE_MAX_RADIUS)
            ((geoauto-v214-valor tangente 'SUCCESS) nil)
            (T (geoauto-v214-valor tangente 'ERROR_CODE))))
      )
    )
    (list
      (cons 'ENTITY entidade) (cons 'HANDLE handle) (cons 'TYPE tipo)
      (cons 'LAYER layer) (cons 'NETWORK_MATCH (if correspondencia T nil))
      (cons 'LAYER_MATCH_MODE correspondencia)
      (cons 'CLOSEST_POINT_WCS nil) (cons 'DISTANCE_2D nil)
      (cons 'DISTANCE_3D nil) (cons 'CLOSEST_POINT_METHOD metodo-projecao)
      (cons 'TANGENT_RESULT nil) (cons 'VALID nil)
      (cons 'REJECTION_REASON 'CLOSEST_POINT_FAILED)
    )
  )
)

(defun geoauto-v214-prioridade-tipo (tipo)
  (cond
    ((= tipo "AcDbLine") 0)
    ((= tipo "AcDbPolyline") 1)
    ((= tipo "AcDb2dPolyline") 2)
    ((= tipo "AcDb3dPolyline") 3)
    ((= tipo "AcDbArc") 4)
    ((= tipo "AcDbSpline") 5)
    (T 99)
  )
)

(defun geoauto-v215-descrever-rejeicao (candidato motivo)
  (list
    (geoauto-v214-valor candidato 'HANDLE)
    (geoauto-v214-valor candidato 'TYPE)
    (geoauto-v214-valor candidato 'LAYER)
    (if motivo motivo (geoauto-v214-valor candidato 'REJECTION_REASON))
    (geoauto-v214-valor candidato 'CLOSEST_POINT_WCS)
    (geoauto-v214-valor candidato 'DISTANCE_2D)
    (geoauto-v214-valor candidato 'CLOSEST_POINT_METHOD)
    (geoauto-v214-valor candidato 'DISTANCE_3D)
  )
)

(defun geoauto-v214-candidato-menor-p
  (a b / da db ma mb ta tb la lb pa pb ha hb)
  (setq da (geoauto-v214-valor a 'DISTANCE_2D))
  (setq db (geoauto-v214-valor b 'DISTANCE_2D))
  (setq ma (geoauto-v214-valor a 'LAYER_MATCH_MODE))
  (setq mb (geoauto-v214-valor b 'LAYER_MATCH_MODE))
  (setq ta (geoauto-v214-valor a 'TYPE))
  (setq tb (geoauto-v214-valor b 'TYPE))
  (setq la (if (eq ma 'EXATO) 0 1))
  (setq lb (if (eq mb 'EXATO) 0 1))
  (setq pa (geoauto-v214-prioridade-tipo ta))
  (setq pb (geoauto-v214-prioridade-tipo tb))
  (setq ha (geoauto-v214-valor a 'HANDLE))
  (setq hb (geoauto-v214-valor b 'HANDLE))
  (if (equal da db 1.0e-12)
    (if (= la lb)
      (if (= pa pb)
        (vl-string-lessp (if ha ha "") (if hb hb ""))
        (< pa pb))
      (< la lb))
    (< da db)
  )
)

(defun geoauto-v214-selecionar-progressivo
  (candidatos exigir-rede / raios raio validos candidato selecionado)
  (setq raios
    (list *geoauto-raio-primario* *geoauto-raio-secundario* *geoauto-raio-maximo*)
  )
  (setq selecionado nil)
  (foreach raio raios
    (if (null selecionado)
      (progn
        (setq validos nil)
        (foreach candidato candidatos
          (if
            (and
              (geoauto-v214-valor candidato 'VALID)
              (numberp (geoauto-v214-valor candidato 'DISTANCE_2D))
              (<= (geoauto-v214-valor candidato 'DISTANCE_2D) raio)
              (if exigir-rede
                (geoauto-v214-valor candidato 'NETWORK_MATCH)
                (not (geoauto-v214-valor candidato 'NETWORK_MATCH)))
            )
            (setq validos (cons candidato validos))
          )
        )
        (if validos
          (setq selecionado
            (list
              (car (vl-sort validos 'geoauto-v214-candidato-menor-p))
              raio
            )
          )
        )
      )
    )
  )
  selecionado
)

(defun geoauto-v217-selecionar-ni-consolidada
  (candidatos / validos candidato ordenados primeiro destino-primeiro
    destino-candidato distancia-primeiro distancia-candidato ambigua)
  ; Inferencia de NI: somente rede consolidada, ate 0.10 m e sem conflito.
  (setq validos nil)
  (foreach candidato candidatos
    (if
      (and
        (geoauto-v214-valor candidato 'VALID)
        (geoauto-v214-valor candidato 'NETWORK_MATCH)
        (numberp (geoauto-v214-valor candidato 'DISTANCE_2D))
        (<=
          (geoauto-v214-valor candidato 'DISTANCE_2D)
          *geoauto-raio-ni-consolidada*)
        (geoauto-layer-texto-por-curva-consolidada
          (geoauto-v214-valor candidato 'LAYER)))
      (setq validos (cons candidato validos))))
  (if (null validos)
    nil
    (progn
      (setq ordenados (vl-sort validos 'geoauto-v214-candidato-menor-p))
      (setq primeiro (car ordenados))
      (setq destino-primeiro
        (geoauto-layer-texto-por-curva-consolidada
          (geoauto-v214-valor primeiro 'LAYER)))
      (setq distancia-primeiro
        (geoauto-v214-valor primeiro 'DISTANCE_2D))
      (setq ambigua nil)
      (foreach candidato (cdr ordenados)
        (setq destino-candidato
          (geoauto-layer-texto-por-curva-consolidada
            (geoauto-v214-valor candidato 'LAYER)))
        (setq distancia-candidato
          (geoauto-v214-valor candidato 'DISTANCE_2D))
        (if
          (and
            destino-candidato
            (not (equal destino-candidato destino-primeiro))
            (<=
              (abs (- distancia-candidato distancia-primeiro))
              *geoauto-tolerancia-ambiguidade*))
          (setq ambigua T)))
      (list primeiro *geoauto-raio-ni-consolidada* ambigua)
    )
  )
)

(defun geoauto-v216-selecionar-estendido-mesma-rede
  (candidatos / validos candidato)
  ; Só entra depois dos três raios normais e nunca atravessa para outra rede.
  (setq validos nil)
  (foreach candidato candidatos
    (if
      (and
        (geoauto-v214-valor candidato 'VALID)
        (geoauto-v214-valor candidato 'NETWORK_MATCH)
        (numberp (geoauto-v214-valor candidato 'DISTANCE_2D))
        (<=
          (geoauto-v214-valor candidato 'DISTANCE_2D)
          *geoauto-raio-estendido-mesma-rede*))
      (setq validos (cons candidato validos))))
  (if validos
    (list
      (car (vl-sort validos 'geoauto-v214-candidato-menor-p))
      *geoauto-raio-estendido-mesma-rede*)
    nil)
)

(defun geoauto-v214-criar-erro-orientacao
  (status rede ponto-csv codigo mensagem candidatos rejeitados)
  (list
    (cons 'STATUS status)
    (cons 'SOURCE_ENTITY nil) (cons 'SOURCE_HANDLE nil)
    (cons 'SOURCE_TYPE nil) (cons 'SOURCE_LAYER nil)
    (cons 'NETWORK rede) (cons 'CSV_POINT_WCS ponto-csv)
    (cons 'CLOSEST_POINT_WCS nil) (cons 'DISTANCE_2D nil)
    (cons 'DISTANCE_3D nil) (cons 'CLOSEST_POINT_METHOD nil)
    (cons 'CURVE_PARAMETER nil) (cons 'CURVE_DISTANCE nil)
    (cons 'TANGENT_METHOD nil) (cons 'TANGENT_XY nil)
    (cons 'READABLE_TANGENT_XY nil) (cons 'NORMAL_XY nil)
    (cons 'GEOMETRIC_ANGLE_RAD nil) (cons 'READABLE_ANGLE_RAD nil)
    (cons 'FLIPPED_180 nil) (cons 'FALLBACKS_ATTEMPTED nil)
    (cons 'ERROR_CODE codigo) (cons 'ERROR_MESSAGE mensagem)
    (cons 'SEARCH_RADIUS nil) (cons 'CROSS_NETWORK nil)
    (cons 'EXTENDED_RADIUS nil)
    (cons 'EXTENDED_RADIUS nil)
    (cons 'CANDIDATES_FOUND candidatos) (cons 'CANDIDATES_REJECTED rejeitados)
    (cons 'PONTO_BASE ponto-csv) (cons 'PONTO_PROXIMO nil)
    (cons 'ANGULO_GEOMETRICO nil) (cons 'ANGULO_TEXTO_FINAL nil)
    (cons 'TANGENTE nil) (cons 'NORMAL nil)
  )
)

(defun geoauto-v215-criar-contingencia-horizontal
  (origem / ponto rede status-origem codigo mensagem)
  ; A contingência existe somente para garantir a identificação do ponto.
  ; Rotation=0 não é confundido com orientação calculada: o status e a causa
  ; originais permanecem explícitos no diagnóstico e no resultado final.
  (setq ponto (geoauto-v214-valor origem 'CSV_POINT_WCS))
  (setq rede (geoauto-v214-valor origem 'NETWORK))
  (setq status-origem (geoauto-v214-valor origem 'STATUS))
  (setq codigo (geoauto-v214-valor origem 'ERROR_CODE))
  (setq mensagem (geoauto-v214-valor origem 'ERROR_MESSAGE))
  (list
    (cons 'STATUS 'ORIENTATION_FALLBACK)
    (cons 'ORIENTATION_SOURCE_STATUS status-origem)
    (cons 'SOURCE_ENTITY nil) (cons 'SOURCE_HANDLE nil)
    (cons 'SOURCE_TYPE nil) (cons 'SOURCE_LAYER nil)
    (cons 'NETWORK rede) (cons 'CSV_POINT_WCS ponto)
    (cons 'CLOSEST_POINT_WCS ponto) (cons 'DISTANCE_2D nil)
    (cons 'DISTANCE_3D nil) (cons 'CLOSEST_POINT_METHOD 'FALLBACK_NONE)
    (cons 'CURVE_PARAMETER nil) (cons 'CURVE_DISTANCE nil)
    (cons 'NEAR_VERTEX nil) (cons 'TANGENT_METHOD 'FALLBACK_HORIZONTAL)
    (cons 'TANGENT_XY '(1.0 0.0 0.0))
    (cons 'READABLE_TANGENT_XY '(1.0 0.0 0.0))
    (cons 'NORMAL_XY '(0.0 1.0 0.0))
    (cons 'GEOMETRIC_ANGLE_RAD 0.0) (cons 'READABLE_ANGLE_RAD 0.0)
    (cons 'FLIPPED_180 nil) (cons 'FALLBACKS_ATTEMPTED nil)
    (cons 'ERROR_CODE codigo) (cons 'ERROR_MESSAGE mensagem)
    (cons 'SEARCH_RADIUS nil) (cons 'CROSS_NETWORK nil)
    (cons 'CANDIDATES_FOUND
      (geoauto-v214-valor origem 'CANDIDATES_FOUND))
    (cons 'CANDIDATES_REJECTED
      (geoauto-v214-valor origem 'CANDIDATES_REJECTED))
    (cons 'PONTO_BASE ponto) (cons 'PONTO_PROXIMO ponto)
    (cons 'ANGULO_GEOMETRICO 0.0) (cons 'ANGULO_TEXTO_FINAL 0.0)
    (cons 'TANGENTE '(1.0 0.0 0.0)) (cons 'NORMAL '(0.0 1.0 0.0))
  )
)

(defun geoauto-v214-resolver-orientacao
  (ponto-csv curvas-por-layer layers-rede rede permitir-outra-rede inferir-ni
   / entidades candidatos rejeitados entidade candidato selecao escolhido
     raio cruzada tangente vetor angulo-geometrico dados-legivel
     angulo-legivel invertido tangente-legivel normal tentativas dentro-raio
     estendida modo-ni layers-avaliacao ambigua-ni destino-texto)
  ; A lista global contém exclusivamente layers catalogados como tubulação.
  (setq modo-ni
    (and
      inferir-ni
      (=
        (geoauto-normalizar-layer rede)
        (geoauto-normalizar-layer *geoauto-rede-nao-identificada*))))
  (setq layers-avaliacao
    (if modo-ni *geoauto-layers-consolidados* layers-rede))
  (setq entidades
    (geoauto-obter-curvas-por-layers
      curvas-por-layer *geoauto-layers-tubulacoes*)
  )
  (setq candidatos nil)
  (setq rejeitados nil)
  (setq dentro-raio 0)
  (foreach entidade entidades
    (setq candidato
      (geoauto-v214-avaliar-entidade
        entidade ponto-csv layers-avaliacao *geoauto-raio-estendido-mesma-rede*)
    )
    (setq candidatos (cons candidato candidatos))
    (if (and (numberp (geoauto-v214-valor candidato 'DISTANCE_2D))
             (<= (geoauto-v214-valor candidato 'DISTANCE_2D)
                 *geoauto-raio-maximo*))
      (setq dentro-raio (1+ dentro-raio)))
    (if (not (geoauto-v214-valor candidato 'VALID))
      (setq rejeitados
        (cons
          (geoauto-v215-descrever-rejeicao candidato nil)
          rejeitados)
      )
    )
  )

  ; Regra forte: esgota a própria rede até 0,70 m antes do fallback.
  (setq selecao nil)
  (setq ambigua-ni nil)
  (if modo-ni
    (progn
      (setq selecao (geoauto-v217-selecionar-ni-consolidada candidatos))
      (if (and selecao (nth 2 selecao))
        (progn
          (setq ambigua-ni T)
          (setq selecao nil))))
    (setq selecao (geoauto-v214-selecionar-progressivo candidatos T)))
  (setq cruzada (if modo-ni T nil))
  (setq estendida nil)
  (if (and (null selecao) (not modo-ni))
    (progn
      (setq selecao
        (geoauto-v216-selecionar-estendido-mesma-rede candidatos))
      (if selecao (setq estendida T))))
  (if (and (null selecao) (not modo-ni) permitir-outra-rede)
    (progn
      (setq selecao (geoauto-v214-selecionar-progressivo candidatos nil))
      (if selecao (setq cruzada T))
    )
  )

  (if (null selecao)
    (if ambigua-ni
      (geoauto-v214-criar-erro-orientacao
        'SKIPPED_AMBIGUOUS_NETWORK rede ponto-csv 'AMBIGUOUS_NI_NETWORK
        "Mais de uma rede consolidada esta a ate 0.10 m da NI."
        dentro-raio (reverse rejeitados))
      (progn
      ; Uma curva valida de outra rede tambem precisa aparecer como rejeitada
      ; quando a politica configuravel nao permite usa-la.
      (foreach candidato candidatos
        (if (geoauto-v214-valor candidato 'VALID)
          (setq rejeitados
            (cons
              (geoauto-v215-descrever-rejeicao
                candidato
                (if (geoauto-v214-valor candidato 'NETWORK_MATCH)
                  'NOT_SELECTED_BY_POLICY
                  'OTHER_NETWORK_DISABLED))
              rejeitados))))
      (if (= dentro-raio 0)
        (geoauto-v214-criar-erro-orientacao
          'SKIPPED_NO_CURVE rede ponto-csv 'SKIPPED_NO_CURVE
          "Nenhuma tubulacao catalogada foi encontrada ate 0.70 m."
          dentro-raio (reverse rejeitados))
        (geoauto-v214-criar-erro-orientacao
          'SKIPPED_NO_ORIENTATION rede ponto-csv 'SKIPPED_NO_ORIENTATION
          "Nenhuma tubulacao permitida ate 0.70 m forneceu orientacao XY."
          dentro-raio (reverse rejeitados))
      )
    ))
    (progn
      (setq escolhido (nth 0 selecao))
      (setq raio (nth 1 selecao))
      (setq destino-texto
        (if modo-ni
          (geoauto-layer-texto-por-curva-consolidada
            (geoauto-v214-valor escolhido 'LAYER))
          nil))
      (foreach candidato candidatos
        (if (and
              (geoauto-v214-valor candidato 'VALID)
              (not (equal
                (geoauto-v214-valor candidato 'ENTITY)
                (geoauto-v214-valor escolhido 'ENTITY))))
          (setq rejeitados
            (cons
              (geoauto-v215-descrever-rejeicao
                candidato
                (if (geoauto-v214-valor candidato 'NETWORK_MATCH)
                  'NOT_SELECTED_HIGHER_DISTANCE
                  'OTHER_NETWORK_NOT_SELECTED))
              rejeitados))))
      (setq tangente (geoauto-v214-valor escolhido 'TANGENT_RESULT))
      (setq vetor (geoauto-v214-valor tangente 'TANGENT_XY))
      (if (null vetor)
        (geoauto-v214-criar-erro-orientacao
          'SKIPPED_NO_ORIENTATION rede ponto-csv
          'REJECTED_INVALID_TANGENT_XY
          "A entidade escolhida nao possui tangente mensuravel em XY."
          (length candidatos) (reverse rejeitados))
        (progn
          (setq angulo-geometrico (atan (cadr vetor) (car vetor)))
          (setq dados-legivel (geoauto-v214-angulo-legivel angulo-geometrico))
          (setq angulo-legivel (nth 0 dados-legivel))
          (setq invertido (nth 1 dados-legivel))
          (setq tangente-legivel
            (list (cos angulo-legivel) (sin angulo-legivel) 0.0))
          (setq normal
            (list (- (sin angulo-legivel)) (cos angulo-legivel) 0.0))
          (setq tentativas (geoauto-v214-valor tangente 'ATTEMPTS))
          (list
            (cons 'STATUS 'ORIENTATION_OK)
            (cons 'SOURCE_ENTITY (geoauto-v214-valor escolhido 'ENTITY))
            (cons 'SOURCE_HANDLE (geoauto-v214-valor escolhido 'HANDLE))
            (cons 'SOURCE_TYPE (geoauto-v214-valor escolhido 'TYPE))
            (cons 'SOURCE_LAYER (geoauto-v214-valor escolhido 'LAYER))
            (cons 'NETWORK rede) (cons 'CSV_POINT_WCS ponto-csv)
            (cons 'CLOSEST_POINT_WCS
              (geoauto-v214-valor escolhido 'CLOSEST_POINT_WCS))
            (cons 'DISTANCE_2D (geoauto-v214-valor escolhido 'DISTANCE_2D))
            (cons 'DISTANCE_3D (geoauto-v214-valor escolhido 'DISTANCE_3D))
            (cons 'CLOSEST_POINT_METHOD
              (geoauto-v214-valor escolhido 'CLOSEST_POINT_METHOD))
            (cons 'CURVE_PARAMETER
              (geoauto-v214-valor tangente 'CURVE_PARAMETER))
            (cons 'CURVE_DISTANCE
              (geoauto-v214-valor tangente 'CURVE_DISTANCE))
            (cons 'NEAR_VERTEX (geoauto-v214-valor tangente 'NEAR_VERTEX))
            (cons 'TANGENT_METHOD
              (geoauto-v214-valor tangente 'TANGENT_METHOD))
            (cons 'TANGENT_XY vetor)
            (cons 'READABLE_TANGENT_XY tangente-legivel)
            (cons 'NORMAL_XY normal)
            (cons 'GEOMETRIC_ANGLE_RAD angulo-geometrico)
            (cons 'READABLE_ANGLE_RAD angulo-legivel)
            (cons 'FLIPPED_180 invertido)
            (cons 'FALLBACKS_ATTEMPTED tentativas)
            (cons 'ERROR_CODE nil) (cons 'ERROR_MESSAGE nil)
            (cons 'SEARCH_RADIUS raio) (cons 'CROSS_NETWORK cruzada)
            (cons 'EXTENDED_RADIUS estendida)
            (cons 'DESTINATION_TEXT_LAYER destino-texto)
            (cons 'NI_NETWORK_INFERRED modo-ni)
            (cons 'CANDIDATES_FOUND dentro-raio)
            (cons 'CANDIDATES_REJECTED (reverse rejeitados))
            ; Aliases somente leitura consumidos pelo posicionamento existente.
            (cons 'PONTO_BASE
              (geoauto-v214-valor escolhido 'CLOSEST_POINT_WCS))
            (cons 'PONTO_PROXIMO
              (geoauto-v214-valor escolhido 'CLOSEST_POINT_WCS))
            (cons 'ANGULO_GEOMETRICO angulo-geometrico)
            (cons 'ANGULO_TEXTO_FINAL angulo-legivel)
            (cons 'TANGENTE tangente-legivel)
            (cons 'NORMAL normal)
          )
        )
      )
    )
  )
)

(defun geoauto-obter-rotacao-texto (entidade / ename dados valor)
  (setq ename
    (cond
      ((= (type entidade) 'ENAME) entidade)
      ((= (type entidade) 'VLA-OBJECT) (vlax-vla-object->ename entidade))
      (T nil)
    )
  )
  (setq dados (if ename (geoauto-chamada-segura 'entget (list ename)) nil))
  (setq valor (if dados (cdr (assoc 50 dados)) nil))
  (if (numberp valor) valor nil)
)

(defun geoauto-rotacao-texto-corresponde-p
  (entidade angulo-esperado / lido diferenca)
  (setq lido (geoauto-obter-rotacao-texto entidade))
  (setq diferenca (geoauto-diferenca-angular lido angulo-esperado))
  (if (and (numberp diferenca)
           (<= diferenca *geoauto-tolerancia-validacao-rotacao*)) T nil)
)

(defun geoauto-v214-validar-rotacao
  (obj angulo-esperado / ename rotation dxf50 dif-rotation dif-dxf sucesso)
  (setq ename
    (if (= (type obj) 'VLA-OBJECT) (vlax-vla-object->ename obj) nil))
  (setq rotation (geoauto-chamada-segura 'vla-get-Rotation (list obj)))
  (setq dxf50 (geoauto-obter-rotacao-texto ename))
  (setq dif-rotation (geoauto-diferenca-angular rotation angulo-esperado))
  (setq dif-dxf (geoauto-diferenca-angular dxf50 angulo-esperado))
  (setq sucesso
    (and
      (numberp dif-rotation) (numberp dif-dxf)
      (<= dif-rotation *geoauto-tolerancia-validacao-rotacao*)
      (<= dif-dxf *geoauto-tolerancia-validacao-rotacao*)))
  (list
    (cons 'SUCCESS sucesso) (cons 'ENAME ename)
    (cons 'EXPECTED angulo-esperado) (cons 'ROTATION_READ rotation)
    (cons 'DXF50_READ dxf50) (cons 'ROTATION_DIFFERENCE dif-rotation)
    (cons 'DXF50_DIFFERENCE dif-dxf))
)

(defun geoauto-v214-registrar-validacao-texto (validacao tipo status tentativas)
  (setq *geoauto-validacoes-texto-atual*
    (append
      *geoauto-validacoes-texto-atual*
      (list
        (append validacao
          (list (cons 'TEXT_TYPE tipo) (cons 'STATUS status)
                (cons 'REAPPLY_ATTEMPTS tentativas))))))
)

(defun geoauto-inserir-texto
  (layer estilo ponto altura texto angulo tipo
   / angulo-cad resultado obj ename xdata-ok validacao tentativas status)
  ; Ponto de entrada único e autoritativo: AddText + Rotation + validação.
  (setq angulo-cad (geoauto-normalizar-angulo-cad angulo))
  (setq tentativas 0)
  (cond
    ((not (numberp angulo-cad))
      (geoauto-v214-registrar-validacao-texto
        (list (cons 'SUCCESS nil) (cons 'EXPECTED nil)
              (cons 'ROTATION_READ nil) (cons 'DXF50_READ nil))
        tipo 'SKIPPED_NO_ORIENTATION tentativas)
      nil
    )
    ((or (null *geoauto-space-atual*) (null ponto)
         (not texto) (= texto "") (not (numberp altura)) (<= altura 0.0))
      (geoauto-v214-registrar-validacao-texto
        (list (cons 'SUCCESS nil) (cons 'EXPECTED angulo-cad)
              (cons 'ROTATION_READ nil) (cons 'DXF50_READ nil))
        tipo 'FAILED_TEXT_CREATION tentativas)
      nil
    )
    (T
      (setq resultado
        (vl-catch-all-apply
          'vla-AddText
          (list *geoauto-space-atual* texto (vlax-3d-point ponto) altura)))
      (if (or (vl-catch-all-error-p resultado) (null resultado))
        (progn
          (geoauto-v214-registrar-validacao-texto
            (list (cons 'SUCCESS nil) (cons 'EXPECTED angulo-cad)
                  (cons 'ROTATION_READ nil) (cons 'DXF50_READ nil))
            tipo 'FAILED_TEXT_CREATION tentativas)
          nil
        )
        (progn
          (setq obj resultado)
          (setq ename (vlax-vla-object->ename obj))
          (vl-catch-all-apply 'vla-put-Layer (list obj layer))
          (if (and estilo (/= estilo ""))
            (vl-catch-all-apply 'vla-put-StyleName (list obj estilo)))
          (if (vlax-property-available-p obj 'Normal T)
            (vl-catch-all-apply
              'vla-put-Normal (list obj (vlax-3d-point '(0.0 0.0 1.0)))))
          (vl-catch-all-apply 'vla-put-Rotation (list obj angulo-cad))
          (setq xdata-ok
            (geoauto-aplicar-xdata
              ename tipo *geoauto-registro-id-atual* *geoauto-codigo-atual*
              *geoauto-ponto-original-atual* *geoauto-execution-id*))
          (vl-catch-all-apply 'vla-Update (list obj))
          (setq validacao (geoauto-v214-validar-rotacao obj angulo-cad))
          ; Uma única reaplicação controlada é permitida.
          (if (not (geoauto-v214-valor validacao 'SUCCESS))
            (progn
              (setq tentativas 1)
              (vl-catch-all-apply 'vla-put-Rotation (list obj angulo-cad))
              (vl-catch-all-apply 'vla-Update (list obj))
              (setq validacao (geoauto-v214-validar-rotacao obj angulo-cad))))
          (cond
            ((not xdata-ok)
              (setq status 'FAILED_TEXT_CREATION)
              (vl-catch-all-apply 'vla-Delete (list obj)))
            ((not (geoauto-v214-valor validacao 'SUCCESS))
              (setq status 'FAILED_ROTATION_PERSISTENCE)
              (vl-catch-all-apply 'vla-Delete (list obj)))
            (T (setq status 'INSERTED_OK))
          )
          (geoauto-v214-registrar-validacao-texto
            validacao tipo status tentativas)
          (if (eq status 'INSERTED_OK) ename nil)
        )
      )
    )
  )
)

(defun geoauto-inserir-registro-fallback-orientado
  (registro estilo altura afastamento
   / contexto layer texto-profundidade texto-diametro resultado quantidade inseridos)
  ; Contingência externa preserva o contexto imutável, inclusive o horizontal.
  (setq contexto *geoauto-contexto-orientacao-atual*)
  (setq quantidade (geoauto-quantidade-textos-registro registro))
  (if (not (geoauto-contexto-rotacao-resolvida-p contexto))
    (list 0 quantidade 0)
    (progn
      (setq layer (nth 0 registro))
      (setq texto-profundidade (nth 2 registro))
      (setq texto-diametro (nth 3 registro))
      (setq resultado
        (geoauto-inserir-contexto-fallback-visual
          layer estilo contexto afastamento altura
          texto-profundidade texto-diametro 0.0 0.0
          'FALLBACK_EXTERNO_ROTACAO_PRESERVADA))
      (if (eq (nth 0 resultado) 'OK)
        (progn
          (setq inseridos 0)
          (if (nth 1 resultado) (setq inseridos (1+ inseridos)))
          (if (nth 2 resultado) (setq inseridos (1+ inseridos)))
          (list
            inseridos
            (- quantidade inseridos)
            (if
              (eq (geoauto-v214-valor contexto 'STATUS) 'ORIENTATION_OK)
              inseridos
              0)))
        (list 0 quantidade 0))
    )
  )
)

(defun geoauto-v214-registrar-diagnostico-final
  (orientacao validacoes status-final / diagnostico)
  (setq diagnostico
    (append orientacao
      (list
        (cons 'RECORD_ID *geoauto-registro-id-atual*)
        (cons 'TOPOGRAPHIC_CODE *geoauto-codigo-atual*)
        (cons 'TEXT_VALIDATIONS validacoes)
        (cons 'FINAL_STATUS status-final))))
  (setq *geoauto-diagnosticos-orientacao*
    (append *geoauto-diagnosticos-orientacao* (list diagnostico)))
  diagnostico
)

(defun geoauto-v214-status-final
  (orientacao validacoes inseridos esperados / status validacao item-status)
  (setq status nil)
  (if (and
        (eq (geoauto-v214-valor orientacao 'STATUS) 'ORIENTATION_FALLBACK)
        (= inseridos esperados))
    (setq status 'INSERTED_FALLBACK_ORIENTATION)
  )
  (if (and (null status)
           (not (geoauto-contexto-rotacao-resolvida-p orientacao)))
    (setq status (geoauto-v214-valor orientacao 'STATUS))
  )
  ; Tentativas temporarias da anticolisao tambem passam pelo escritor e ficam
  ; registradas. Se todas as entidades finais existem, elas nao podem rebaixar
  ; um registro bem-sucedido por causa de uma tentativa temporaria descartada.
  (if (and (null status) (= inseridos esperados))
    (setq status 'INSERTED_OK)
  )
  (if (null status)
    (progn
      (foreach validacao validacoes
        (setq item-status (geoauto-v214-valor validacao 'STATUS))
        (cond
          ((eq item-status 'FAILED_ROTATION_PERSISTENCE)
            (setq status 'FAILED_ROTATION_PERSISTENCE))
          ((and (null status) (eq item-status 'FAILED_TEXT_CREATION))
            (setq status 'FAILED_TEXT_CREATION)))
      )
      (if (null status)
        (setq status
          (cond
            ((> inseridos 0) 'PARTIAL_TEXT_INSERTION)
            (T 'FAILED_TEXT_CREATION))))
    )
  )
  status
)

(defun geoauto-v215-rejeicao-menor-p (a b / da db)
  (setq da (nth 5 a))
  (setq db (nth 5 b))
  (cond
    ((and (numberp da) (numberp db)) (< da db))
    ((numberp da) T)
    (T nil)
  )
)

(defun geoauto-v214-imprimir-diagnostico
  (item / validacoes validacao rejeitados rejeitado tentativas tentativa
    contador limite omitidos)
  (princ
    (strcat
      "\nRegistro=" (if (geoauto-v214-valor item 'RECORD_ID)
        (geoauto-v214-valor item 'RECORD_ID) "")
      " | codigo=" (if (geoauto-v214-valor item 'TOPOGRAPHIC_CODE)
        (geoauto-v214-valor item 'TOPOGRAPHIC_CODE) "")
      " | rede=" (if (geoauto-v214-valor item 'NETWORK)
        (geoauto-v214-valor item 'NETWORK) "")
      " | status=" (vl-princ-to-string (geoauto-v214-valor item 'FINAL_STATUS))
      " | origem="
        (vl-princ-to-string
          (geoauto-v214-valor item 'ORIENTATION_SOURCE_STATUS))
      "\n  WCS=" (vl-princ-to-string (geoauto-v214-valor item 'CSV_POINT_WCS))
      " | raio=" (vl-princ-to-string (geoauto-v214-valor item 'SEARCH_RADIUS))
      " | candidatos=" (vl-princ-to-string (geoauto-v214-valor item 'CANDIDATES_FOUND))
      " | outraRede=" (vl-princ-to-string (geoauto-v214-valor item 'CROSS_NETWORK))
      " | estendida=" (vl-princ-to-string (geoauto-v214-valor item 'EXTENDED_RADIUS))
      "\n  handle=" (vl-princ-to-string (geoauto-v214-valor item 'SOURCE_HANDLE))
      " | tipo=" (vl-princ-to-string (geoauto-v214-valor item 'SOURCE_TYPE))
      " | layer=" (vl-princ-to-string (geoauto-v214-valor item 'SOURCE_LAYER))
      "\n  closest=" (vl-princ-to-string (geoauto-v214-valor item 'CLOSEST_POINT_WCS))
      " | distancia2d=" (vl-princ-to-string (geoauto-v214-valor item 'DISTANCE_2D))
      " | distancia3d=" (vl-princ-to-string (geoauto-v214-valor item 'DISTANCE_3D))
      " | projecao="
        (vl-princ-to-string (geoauto-v214-valor item 'CLOSEST_POINT_METHOD))
      " | parametro=" (vl-princ-to-string (geoauto-v214-valor item 'CURVE_PARAMETER))
      " | distCurva=" (vl-princ-to-string (geoauto-v214-valor item 'CURVE_DISTANCE))
      "\n  vertice=" (vl-princ-to-string (geoauto-v214-valor item 'NEAR_VERTEX))
      " | metodo=" (vl-princ-to-string (geoauto-v214-valor item 'TANGENT_METHOD))
      " | tangenteXY=" (vl-princ-to-string (geoauto-v214-valor item 'TANGENT_XY))
      "\n  geometrico=" (vl-princ-to-string (geoauto-v214-valor item 'GEOMETRIC_ANGLE_RAD))
      " | legivel=" (vl-princ-to-string (geoauto-v214-valor item 'READABLE_ANGLE_RAD))
      " | flip180=" (vl-princ-to-string (geoauto-v214-valor item 'FLIPPED_180))
      "\n  erro=" (vl-princ-to-string (geoauto-v214-valor item 'ERROR_CODE))
      " | mensagem=" (vl-princ-to-string (geoauto-v214-valor item 'ERROR_MESSAGE))))
  (setq rejeitados (geoauto-v214-valor item 'CANDIDATES_REJECTED))
  (if rejeitados
    (setq rejeitados (vl-sort rejeitados 'geoauto-v215-rejeicao-menor-p)))
  (setq contador 0)
  (setq limite 3)
  (foreach rejeitado rejeitados
    (if (< contador limite)
      (princ
        (strcat
          "\n  rejeitado: handle=" (vl-princ-to-string (nth 0 rejeitado))
          " | tipo=" (vl-princ-to-string (nth 1 rejeitado))
          " | layer=" (vl-princ-to-string (nth 2 rejeitado))
          " | motivo=" (vl-princ-to-string (nth 3 rejeitado))
          " | closest=" (vl-princ-to-string (nth 4 rejeitado))
          " | d2=" (vl-princ-to-string (nth 5 rejeitado))
          " | metodo=" (vl-princ-to-string (nth 6 rejeitado))
          " | d3=" (vl-princ-to-string (nth 7 rejeitado)))))
    (setq contador (1+ contador)))
  (setq omitidos (- contador limite))
  (if (> omitidos 0)
    (princ
      (strcat "\n  ... " (itoa omitidos)
        " candidato(s) rejeitado(s) adicional(is) omitido(s).")))
  (setq tentativas (geoauto-v214-valor item 'FALLBACKS_ATTEMPTED))
  (foreach tentativa tentativas
    (princ
      (strcat
        "\n  tangente: metodo="
          (vl-princ-to-string (geoauto-v214-valor tentativa 'METHOD))
        " | sucesso="
          (vl-princ-to-string (geoauto-v214-valor tentativa 'SUCCESS))
        " | motivo="
          (vl-princ-to-string (geoauto-v214-valor tentativa 'FAILURE_REASON)))))
  (setq validacoes (geoauto-v214-valor item 'TEXT_VALIDATIONS))
  (foreach validacao validacoes
    (princ
      (strcat
        "\n  TEXT=" (vl-princ-to-string (geoauto-v214-valor validacao 'TEXT_TYPE))
        " | esperado=" (vl-princ-to-string (geoauto-v214-valor validacao 'EXPECTED))
        " | DXF50=" (vl-princ-to-string (geoauto-v214-valor validacao 'DXF50_READ))
        " | Rotation=" (vl-princ-to-string (geoauto-v214-valor validacao 'ROTATION_READ))
        " | difDXF50="
          (vl-princ-to-string (geoauto-v214-valor validacao 'DXF50_DIFFERENCE))
        " | difRotation="
          (vl-princ-to-string (geoauto-v214-valor validacao 'ROTATION_DIFFERENCE))
        " | reaplicacoes="
          (vl-princ-to-string (geoauto-v214-valor validacao 'REAPPLY_ATTEMPTS))
        " | status=" (vl-princ-to-string (geoauto-v214-valor validacao 'STATUS)))))
  (princ)
)

(defun c:GEOAUTODIAGNOSTICOROTACAO (/ item)
  (princ
    (strcat "\nGeoAutoEngine Orientation Runtime "
      *geoauto-orientation-runtime-version*))
  (foreach item *geoauto-diagnosticos-orientacao*
    (geoauto-v214-imprimir-diagnostico item))
  (princ)
)

(defun geoauto-v216-obter-xdata-string
  (ename prefixo / dados bloco item texto valor)
  (setq valor nil)
  (if ename
    (progn
      (setq dados (entget ename (list *geoauto-xdata-app*)))
      (setq bloco (assoc -3 dados))
      (if bloco
        (foreach item (cdr (cadr bloco))
          (if (and
                (null valor)
                (= (car item) 1000)
                (setq texto (cdr item))
                (= (substr texto 1 (strlen prefixo)) prefixo))
            (setq valor (substr texto (1+ (strlen prefixo))))))))
  )
  valor
)

(defun c:GEOAUTODIAGNOSTICOFALLBACK (/ item total impressos limite)
  (setq total 0)
  (setq impressos 0)
  (setq limite 20)
  (foreach item *geoauto-diagnosticos-orientacao*
    (if (eq
          (geoauto-v214-valor item 'FINAL_STATUS)
          'INSERTED_FALLBACK_ORIENTATION)
      (progn
        (setq total (1+ total))
        (if (< impressos limite)
          (progn
            (geoauto-v214-imprimir-diagnostico item)
            (setq impressos (1+ impressos)))))))
  (princ
    (strcat
      "\nFallbacks encontrados: " (itoa total)
      " | exibidos: " (itoa impressos)))
  (if (> total impressos)
    (princ "\nOs demais foram omitidos para preservar o historico do CAD."))
  (princ)
)

(defun c:GEOAUTOVALIDARROTACAO
  (/ selecao ename registro-xdata encontrado item validacoes validacao)
  (setq selecao (entsel "\nSelecione um TEXT criado pelo GeoAutoEngine: "))
  (setq ename (if selecao (car selecao) nil))
  (setq registro-xdata
    (if ename
      (geoauto-v216-obter-xdata-string ename "REGISTRO=")
      nil))
  (setq encontrado nil)
  (if ename
    (foreach item *geoauto-diagnosticos-orientacao*
      (setq validacoes (geoauto-v214-valor item 'TEXT_VALIDATIONS))
      (foreach validacao validacoes
        (if (and
              (not encontrado)
              (equal ename (geoauto-v214-valor validacao 'ENAME)))
          (progn
            (setq encontrado T)
            (geoauto-v214-imprimir-diagnostico item)))))
  )
  ; Fallback por identidade persistente: o ENAME pode ser reconstruído pelo
  ; Civil 3D, mas REGISTRO permanece no XData do TEXT.
  (if (and (not encontrado) registro-xdata)
    (foreach item *geoauto-diagnosticos-orientacao*
      (if (and
            (not encontrado)
            (equal
              registro-xdata
              (geoauto-v214-valor item 'RECORD_ID)))
        (progn
          (setq encontrado T)
          (geoauto-v214-imprimir-diagnostico item)))))
  (if (not encontrado)
    (princ
      (strcat
        "\nNenhum diagnostico da execucao atual corresponde ao TEXT."
        " Registro XData=" (if registro-xdata registro-xdata "<ausente>"))))
  (princ)
)

(defun c:GEOAUTOPROF
  (/ *error* acad doc space undo-open coleta curvas-abertas curvas-por-layer
     curvas-interferencia curvas-fechadas-ignoradas layers-abertos layers-fechados
     layers-curvas-desenho curvas-abertas-indexadas registro layer resultado
     quantidade-registro total-textos inseridos rotacionados
     sem-entidade fora-raio rotacao-padrao erros-tangente
     erros-insercao associados-abertas fallbacks
     pluviais-rotacionados pluviais-sem-curva
     sem-entidade-textos fora-raio-textos instrucao-bloco
     resultado-bloco status-bloco block-key blocos-inseridos
     blocos-por-tipo falhas-blocos arquivos-ausentes-runtime
     blocos-rotacao-rede blocos-rotacao-fallback
     blocos-sobrepostos-ignorados pontos-blocos-ocupados margem-colisao
     textos-existentes-considerados obstaculos-pv-registrados grupos-processados
     grupos-posicao-original grupos-frente grupos-tras
     tentativas-reposicionamento colisoes-detectadas
     posicionamentos-forcados maior-deslocamento valor-deslocamento
     reposicionados-total curvas-rejeitadas-distancia
     amostra-distancias-curvas textos-sem-curva-limite
     grupos-afastados-perpendicular tentativas-perpendiculares
     interferencias-curvas maior-afastamento-adicional
     valor-afastamento posicionamentos-forcados-curva
     resultado-fase estilo-ativo estilo-anotacao-divisa-ativo
     indice-registro fallback-direto
     fallbacks-bbox valor-fallback-bbox registros-associados-distancia
     registros-sem-curva-limite registros-rotacao-padrao
     associados-raio-primario associados-raio-secundario associados-raio-maximo
     associacoes-ambiguas registros-por-rede associados-por-rede
     amostra-sem-associacao amostra-raio-secundario amostra-raio-maximo
     amostra-ambiguas faixa-associacao valor-ambiguidade
     resultado-limpeza objetos-antigos-encontrados objetos-antigos-removidos
     falhas-remocao-objetos textos-usuario-ignorados execucao-duplicada-evitada
     circulos-atencao-inseridos falhas-circulos-atencao resultado-atencao
     circulos-atencao-sobrepostos-ignorados
     alerta-codigo-desconhecido alertas-diagnostico-inseridos
     falhas-alertas-diagnostico
     anotacao-divisa resultado-anotacao-divisa
     anotacoes-divisa-inseridas falhas-anotacoes-divisa
     estrutura-pluvial resultado-estrutura-pluvial
     estruturas-pluviais-inseridas falhas-estruturas-pluviais
     estruturas-pluviais-ocupadas
     estruturas-pluviais-sobrepostas-ignoradas
     grelhas-inseridas bocas-lobo-inseridas
     falhas-grelhas falhas-bocas-lobo
     alerta-estrutura-pluvial alertas-estruturas-pluviais-inseridos
     falhas-alertas-estruturas-pluviais
     geracao-permitida tabela-curvas-abertas tabela-curvas-fechadas
     tabela-entidades-nao-suportadas referencias-diagnostico
     entidades-nao-suportadas-por-layer curvas-fechadas-por-layer
     blocos-referenciados xrefs-referenciadas agua-processada
     agua-rotacionada agua-sem-curva-permitida agua-fora-raio
     agua-erros-tangente agua-layer-nao-reconhecido agua-referencia-aninhada
     agua-layer-xref-prefixado motivos-fallback-agua motivo-agua
     amostra-agua-rotacionada amostra-agua-fallback amostra-orientacao
     rotacoes-preservadas-fallback-visual falhas-bbox-posicionamento
     falhas-anticolisao-posicionamento tentativas-rotacao-original-mantida
     rotacoes-texto-confirmadas rotacoes-texto-divergentes fallbacks-visuais
     rotacoes-preservadas-fallback-externo)
  (vl-load-com)
  (setq acad (vlax-get-acad-object))
  (setq doc (vla-get-ActiveDocument acad))
  (setq space
    (if (= 1 (getvar "CVPORT"))
      (vla-get-PaperSpace doc)
      (vla-get-ModelSpace doc)
    )
  )
  (setq *geoauto-space-atual* space)
  (setq undo-open nil)
  (setq objetos-antigos-encontrados 0)
  (setq objetos-antigos-removidos 0)
  (setq falhas-remocao-objetos 0)
  (setq textos-usuario-ignorados 0)
  (setq execucao-duplicada-evitada 0)
  (setq circulos-atencao-inseridos 0)
  (setq falhas-circulos-atencao 0)
  (setq circulos-atencao-sobrepostos-ignorados 0)
  (setq alertas-diagnostico-inseridos 0)
  (setq falhas-alertas-diagnostico 0)
  (setq anotacoes-divisa-inseridas 0)
  (setq falhas-anotacoes-divisa 0)
  (setq estruturas-pluviais-inseridas 0)
  (setq falhas-estruturas-pluviais 0)
  (setq estruturas-pluviais-ocupadas nil)
  (setq estruturas-pluviais-sobrepostas-ignoradas 0)
  (setq grelhas-inseridas 0)
  (setq bocas-lobo-inseridas 0)
  (setq falhas-grelhas 0)
  (setq falhas-bocas-lobo 0)
  (setq alertas-estruturas-pluviais-inseridos 0)
  (setq falhas-alertas-estruturas-pluviais 0)
  (setq geracao-permitida T)
  (setq total-textos 0)
  (setq inseridos 0)
  (setq rotacionados 0)
  (setq sem-entidade 0)
  (setq fora-raio 0)
  (setq rotacao-padrao 0)
  (setq erros-tangente 0)
  (setq erros-insercao 0)
  (setq associados-abertas 0)
  (setq fallbacks 0)
  (setq pluviais-rotacionados 0)
  (setq pluviais-sem-curva 0)
  (setq sem-entidade-textos 0)
  (setq fora-raio-textos 0)
  (setq blocos-inseridos 0)
  (setq blocos-por-tipo nil)
  (setq falhas-blocos nil)
  (setq arquivos-ausentes-runtime nil)
  (setq blocos-rotacao-rede 0)
  (setq blocos-rotacao-fallback 0)
  (setq blocos-sobrepostos-ignorados 0)
  (setq pontos-blocos-ocupados nil)
  (setq textos-existentes-considerados 0)
  (setq obstaculos-pv-registrados 0)
  (setq grupos-processados 0)
  (setq grupos-posicao-original 0)
  (setq grupos-frente 0)
  (setq grupos-tras 0)
  (setq tentativas-reposicionamento 0)
  (setq colisoes-detectadas 0)
  (setq posicionamentos-forcados 0)
  (setq maior-deslocamento 0.0)
  (setq curvas-rejeitadas-distancia 0)
  (setq amostra-distancias-curvas nil)
  (setq textos-sem-curva-limite 0)
  (setq grupos-afastados-perpendicular 0)
  (setq tentativas-perpendiculares 0)
  (setq interferencias-curvas 0)
  (setq maior-afastamento-adicional 0.0)
  (setq posicionamentos-forcados-curva 0)
  (setq fallbacks-bbox 0)
  (setq registros-associados-distancia 0)
  (setq registros-sem-curva-limite 0)
  (setq registros-rotacao-padrao 0)
  (setq associados-raio-primario 0)
  (setq associados-raio-secundario 0)
  (setq associados-raio-maximo 0)
  (setq associacoes-ambiguas 0)
  (setq registros-por-rede nil)
  (setq associados-por-rede nil)
  (setq amostra-sem-associacao nil)
  (setq amostra-raio-secundario nil)
  (setq amostra-raio-maximo nil)
  (setq amostra-ambiguas nil)
  (setq agua-processada 0)
  (setq agua-rotacionada 0)
  (setq agua-sem-curva-permitida 0)
  (setq agua-fora-raio 0)
  (setq agua-erros-tangente 0)
  (setq agua-layer-nao-reconhecido 0)
  (setq agua-referencia-aninhada 0)
  (setq agua-layer-xref-prefixado 0)
  (setq motivos-fallback-agua nil)
  (setq amostra-agua-rotacionada nil)
  (setq amostra-agua-fallback nil)
  (setq amostra-orientacao nil)
  (setq rotacoes-preservadas-fallback-visual 0)
  (setq falhas-bbox-posicionamento 0)
  (setq falhas-anticolisao-posicionamento 0)
  (setq tentativas-rotacao-original-mantida 0)
  (setq rotacoes-texto-confirmadas 0)
  (setq rotacoes-texto-divergentes 0)
  (setq fallbacks-visuais 0)
  (setq rotacoes-preservadas-fallback-externo 0)
  (setq *geoauto-contexto-orientacao-atual* nil)
  (setq *geoauto-diagnosticos-orientacao* nil)
  (setq *geoauto-validacoes-texto-atual* nil)
  (setq tabela-curvas-abertas nil)
  (setq tabela-curvas-fechadas nil)
  (setq tabela-entidades-nao-suportadas nil)
  (setq referencias-diagnostico nil)
  (setq entidades-nao-suportadas-por-layer nil)
  (setq curvas-fechadas-por-layer nil)
  (setq blocos-referenciados 0)
  (setq xrefs-referenciadas 0)
  (setq indice-registro 0)
  (setq estilo-ativo *geoauto-estilo-texto*)
  (setq estilo-anotacao-divisa-ativo *geoauto-estilo-anotacao-divisa*)
  (setq *geoauto-text-occupied-boxes* nil)
  (setq *geoauto-curve-bboxes* nil)
  (setq *geoauto-fase-atual* "INICIALIZACAO")

  (defun *error* (message)
    (if undo-open
      (progn
        (vla-EndUndoMark doc)
        (setq undo-open nil)
      )
    )
    (if (and message
             (/= message "Function cancelled")
             (/= message "quit / exit abort"))
      (princ (strcat "\nGeoAutoEngine: " message))
    )
    (princ (strcat "\nFase: " *geoauto-fase-atual*))
    (princ)
  )

  (setq *geoauto-fase-atual* "UNDO")
  (setq resultado-fase
    (vl-catch-all-apply 'geoauto-iniciar-undo (list doc))
  )
  (if (not (vl-catch-all-error-p resultado-fase))
    (setq undo-open T)
  )

  (setq *geoauto-fase-atual* "ESTILO")
  (geoauto-debug "Validando estilo de texto...")
  (setq resultado-fase
    (vl-catch-all-apply
      'geoauto-validar-estilo
      (list doc
        "GEO_ARIAL"
        "Arial"
        "arial.ttf"
        :vlax-false
        :vlax-false
      )
    )
  )
  (if (vl-catch-all-error-p resultado-fase)
    (progn
      (setq *geoauto-estilo-texto* "Standard")
      (geoauto-debug "Falha no estilo GEO_ARIAL; usando Standard.")
    )
    (setq estilo-ativo *geoauto-estilo-texto*)
  )

  (foreach layer *geoauto-layers-estruturas-pluviais*
    (setq resultado-fase
      (vl-catch-all-apply 'geoauto-validar-layer (list doc layer))
    )
    (if (vl-catch-all-error-p resultado-fase)
      (geoauto-debug
        (strcat "Layer pluvial nao pode ser validado: " layer)
      )
    )
  )

  (if *geoauto-anotacoes-divisa*
    (progn
      (setq *geoauto-fase-atual* "ESTILO_ANOTACAO_DIVISA")
      (geoauto-debug "Validando estilo Arial Bold para anotacoes de divisa...")
      (setq resultado-fase
        (vl-catch-all-apply
          'geoauto-validar-estilo
          (list doc
            "GEO_ARIAL_BOLD"
            "Arial"
            "arialbd.ttf"
            :vlax-true
            :vlax-false
          )
        )
      )
      (if (vl-catch-all-error-p resultado-fase)
        (progn
          (setq estilo-anotacao-divisa-ativo estilo-ativo)
          (geoauto-debug
            "Falha no estilo GEO_ARIAL_BOLD; usando o estilo de texto padrao."
          )
        )
        (setq estilo-anotacao-divisa-ativo
          *geoauto-estilo-anotacao-divisa*
        )
      )
    )
  )

  (setq *geoauto-fase-atual* "LAYERS")
  (geoauto-debug "Validando layers de saída...")
  (foreach layer *geoauto-layers-texto*
    (setq resultado-fase
      (vl-catch-all-apply 'geoauto-validar-layer (list doc layer))
    )
    (if (vl-catch-all-error-p resultado-fase)
      (geoauto-debug (strcat "Layer não pôde ser validado: " layer))
    )
  )

  ; Idempotência por Lisp: remove somente objetos GEOAUTOENGINE que possuem
  ; o mesmo EXECUCAO deste arquivo. Outras importações e objetos do usuário
  ; permanecem intocados.
  (if *geoauto-anotacoes-divisa*
    (progn
      (setq resultado-fase
        (vl-catch-all-apply
          'geoauto-configurar-layer-cor
          (list
            doc
            *geoauto-layer-anotacao-divisa*
            *geoauto-cor-anotacao-divisa*
          )
        )
      )
      (if (or
            (vl-catch-all-error-p resultado-fase)
            (null resultado-fase))
        (geoauto-debug
          "Nao foi possivel configurar a layer -Texto na cor branca."
        )
      )
    )
  )

  (setq *geoauto-fase-atual* "LIMPEZA_OBJETOS_ANTERIORES")
  (regapp *geoauto-xdata-app*)
  (setq resultado-limpeza
    (vl-catch-all-apply 'geoauto-remover-objetos-anteriores (list space))
  )
  (if (vl-catch-all-error-p resultado-limpeza)
    (progn
      (setq geracao-permitida nil)
      (setq falhas-remocao-objetos 1)
      (geoauto-debug
        "Falha ao verificar objetos anteriores; nova inserção bloqueada para evitar duplicação."
      )
    )
    (progn
      (setq objetos-antigos-encontrados (nth 0 resultado-limpeza))
      (setq objetos-antigos-removidos (nth 1 resultado-limpeza))
      (setq falhas-remocao-objetos (nth 2 resultado-limpeza))
      (if (> objetos-antigos-encontrados 0)
        (setq execucao-duplicada-evitada 1)
      )
      (if (> falhas-remocao-objetos 0)
        (progn
          (setq geracao-permitida nil)
          (geoauto-debug
            "Objetos anteriores não removidos; nova inserção bloqueada para evitar duplicação."
          )
        )
      )
    )
  )

  (setq *geoauto-text-occupied-boxes* nil)
  (setq textos-usuario-ignorados (geoauto-contar-textos-usuario space))

  ; Por padrão, a colisão considera somente grupos aceitos na execução atual.
  ; A coleta opcional considera apenas XData TIPO=OBSTACULO explícito.
  (setq *geoauto-fase-atual* "COLETA_TEXTOS_EXISTENTES")
  (setq margem-colisao
    (* *geoauto-altura-texto* *geoauto-fator-margem-colisao*)
  )
  (setq resultado-fase
    (vl-catch-all-apply
      'geoauto-coletar-textos-existentes-seguro
      (list space *geoauto-layers-texto* margem-colisao)
    )
  )
  (if (vl-catch-all-error-p resultado-fase)
    (progn
      (setq *geoauto-text-occupied-boxes* nil)
      (setq textos-existentes-considerados 0)
      (geoauto-debug "Coleta opcional de obstáculos falhou; seguindo sem obstáculos prévios.")
    )
    (setq textos-existentes-considerados resultado-fase)
  )

  ; A geometria é percorrida uma única vez. Se a coleta falhar, os TEXTs usam
  ; a contingência explícita; a fase independente de blocos ainda prossegue.
  (setq *geoauto-fase-atual* "COLETA_CURVAS")
  (geoauto-debug "Coletando curvas abertas...")
  (setq resultado-fase
    (vl-catch-all-apply
      'geoauto-coletar-curvas-seguro
      (list space)
    )
  )
  (if (vl-catch-all-error-p resultado-fase)
    (progn
      (setq coleta (list nil 0 nil nil 0 nil nil nil nil nil nil nil nil nil 0 0))
      (geoauto-debug "Coleta de curvas falhou; TEXTs usarão contingência explícita.")
    )
    (setq coleta resultado-fase)
  )
  (setq curvas-abertas (nth 0 coleta))
  (setq curvas-fechadas-ignoradas (nth 1 coleta))
  (setq layers-abertos (nth 2 coleta))
  (setq layers-fechados (nth 3 coleta))
  (setq curvas-abertas-indexadas (nth 4 coleta))
  (setq layers-curvas-desenho (nth 5 coleta))
  (setq curvas-interferencia (nth 6 coleta))
  (setq curvas-por-layer (nth 7 coleta))
  (setq tabela-curvas-abertas (nth 8 coleta))
  (setq tabela-curvas-fechadas (nth 9 coleta))
  (setq tabela-entidades-nao-suportadas (nth 10 coleta))
  (setq referencias-diagnostico (nth 11 coleta))
  (setq pontos-blocos-ocupados
    (geoauto-coletar-pontos-blocos-existentes referencias-diagnostico)
  )
  (setq estruturas-pluviais-ocupadas
    (geoauto-coletar-estruturas-pluviais-existentes space)
  )
  (setq *geoauto-pontos-circulos-atencao*
    (geoauto-coletar-circulos-atencao-existentes space)
  )
  (setq obstaculos-pv-registrados
    (geoauto-registrar-obstaculos-pv
      referencias-diagnostico
      *geoauto-instrucoes-blocos*
      *geoauto-raio-protecao-texto-pv*
      margem-colisao
    )
  )
  (setq entidades-nao-suportadas-por-layer (nth 12 coleta))
  (setq curvas-fechadas-por-layer (nth 13 coleta))
  (setq blocos-referenciados (nth 14 coleta))
  (setq xrefs-referenciadas (nth 15 coleta))

  (setq *geoauto-fase-atual* "INSERCAO_TEXTOS")
  (geoauto-debug
    (strcat "Iniciando textos. Registros: " (itoa (length *geoauto-registros*)))
  )
  (if geracao-permitida
    (progn
  (foreach registro *geoauto-registros*
    ; O contexto pertence exclusivamente ao registro atual.
    (setq *geoauto-contexto-orientacao-atual* nil)
    (setq *geoauto-validacoes-texto-atual* nil)
    (setq *geoauto-registro-id-atual* (nth 6 registro))
    (setq *geoauto-codigo-atual* (nth 7 registro))
    (setq *geoauto-ponto-original-atual* (nth 1 registro))
    (setq indice-registro (1+ indice-registro))
    (if (and *geoauto-debug*
             (= (rem indice-registro *geoauto-intervalo-progresso*) 0))
      (geoauto-debug
        (strcat "Progresso de textos: " (itoa indice-registro))
      )
    )
    (setq quantidade-registro
      (geoauto-quantidade-textos-registro registro)
    )
    (setq total-textos (+ total-textos quantidade-registro))
    (setq registros-por-rede
      (geoauto-contador-incrementar (nth 4 registro) registros-por-rede)
    )
    (if (geoauto-rede-agua-p (nth 4 registro))
      (setq agua-processada (1+ agua-processada))
    )
    (setq resultado
      (vl-catch-all-apply
        'geoauto-inserir-registro
        (list
          registro curvas-por-layer curvas-abertas
          curvas-fechadas-por-layer entidades-nao-suportadas-por-layer
          referencias-diagnostico curvas-interferencia *geoauto-estilo-texto*
          *geoauto-altura-texto* *geoauto-afastamento-texto*
          *geoauto-raio-primario* *geoauto-raio-secundario*
          *geoauto-raio-maximo* *geoauto-tolerancia-ambiguidade*
          *geoauto-amostra-tangente*
          *geoauto-passo-deslocamento-longitudinal*
          *geoauto-max-deslocamento-longitudinal*
          *geoauto-fator-margem-colisao*
          *geoauto-passo-afastamento-perpendicular*
          *geoauto-max-afastamento-perpendicular-adicional*
          *geoauto-fator-margem-polyline-texto*
          *geoauto-verificar-interferencia-polylines*
          *geoauto-habilitar-rotacao*
          *geoauto-habilitar-colisao-textos*
        )
      )
    )
    (if (vl-catch-all-error-p resultado)
      (progn
        ; Última contingência por registro. Se a orientação já foi resolvida,
        ; o fallback externo reutiliza o contexto e preserva a Rotation.
        (if (null *geoauto-contexto-orientacao-atual*)
          (setq *geoauto-contexto-orientacao-atual*
            (geoauto-v214-criar-erro-orientacao
              'SKIPPED_NO_ORIENTATION (nth 4 registro) (nth 1 registro)
              'UNEXPECTED_RECORD_ERROR
              "Falha inesperada antes da conclusao da orientacao do registro."
              0 nil)))
        (if (not
              (geoauto-contexto-rotacao-resolvida-p
                *geoauto-contexto-orientacao-atual*))
          (setq *geoauto-contexto-orientacao-atual*
            (geoauto-v215-criar-contingencia-horizontal
              *geoauto-contexto-orientacao-atual*)))
        (setq fallback-direto
          (vl-catch-all-apply
            'geoauto-inserir-registro-fallback-orientado
            (list registro *geoauto-estilo-texto* *geoauto-altura-texto*
                  *geoauto-afastamento-texto*)
          )
        )
        (if (vl-catch-all-error-p fallback-direto)
          (setq erros-insercao
            (+ erros-insercao quantidade-registro)
          )
          (progn
            (setq inseridos (+ inseridos (nth 0 fallback-direto)))
            (setq erros-insercao
              (+ erros-insercao (nth 1 fallback-direto))
            )
            (if (> (nth 2 fallback-direto) 0)
              (progn
                (setq rotacionados
                  (+ rotacionados (nth 2 fallback-direto))
                )
                (setq rotacoes-preservadas-fallback-externo
                  (+ rotacoes-preservadas-fallback-externo
                     (nth 2 fallback-direto))
                )
              )
              (progn
                (setq rotacao-padrao
                  (+ rotacao-padrao (nth 0 fallback-direto))
                )
                (setq registros-rotacao-padrao
                  (1+ registros-rotacao-padrao)
                )
              )
            )
          )
        )
        ; Mesmo uma excecao inesperada por registro produz diagnostico final.
        ; O fallback externo usa o contexto V2.1.7 quando ele já existe.
        (geoauto-v214-registrar-diagnostico-final
          *geoauto-contexto-orientacao-atual*
          *geoauto-validacoes-texto-atual*
          (geoauto-v214-status-final
            *geoauto-contexto-orientacao-atual*
            *geoauto-validacoes-texto-atual*
            (if (vl-catch-all-error-p fallback-direto)
              0
              (nth 0 fallback-direto))
            quantidade-registro))
        (setq fallbacks (+ fallbacks quantidade-registro))
        (setq grupos-processados (1+ grupos-processados))
        (if (geoauto-rede-agua-p (nth 4 registro))
          (if (and
                (geoauto-contexto-rotacao-resolvida-p
                  *geoauto-contexto-orientacao-atual*)
                (not (vl-catch-all-error-p fallback-direto))
                (> (nth 2 fallback-direto) 0))
            (setq agua-rotacionada (1+ agua-rotacionada))
            (progn
              (setq agua-sem-curva-permitida
                (1+ agua-sem-curva-permitida)
              )
              (setq motivos-fallback-agua
                (geoauto-contador-incrementar
                  "ERRO_DESCONHECIDO" motivos-fallback-agua
                )
              )
            )
          )
        )
      )
      (progn
        (setq inseridos (+ inseridos (nth 0 resultado)))
        (setq rotacionados (+ rotacionados (nth 1 resultado)))
        (setq sem-entidade (+ sem-entidade (nth 2 resultado)))
        (setq fora-raio (+ fora-raio (nth 3 resultado)))
        (setq rotacao-padrao (+ rotacao-padrao (nth 4 resultado)))
        (setq erros-tangente (+ erros-tangente (nth 5 resultado)))
        (setq erros-insercao (+ erros-insercao (nth 6 resultado)))
        (setq associados-abertas
          (+ associados-abertas (nth 7 resultado))
        )
        (setq fallbacks (+ fallbacks (nth 8 resultado)))
        (if (> (nth 60 resultado) 0)
          (setq rotacoes-preservadas-fallback-visual
            (+ rotacoes-preservadas-fallback-visual (nth 0 resultado))
          )
        )
        (setq falhas-bbox-posicionamento
          (+ falhas-bbox-posicionamento (nth 61 resultado))
        )
        (setq falhas-anticolisao-posicionamento
          (+ falhas-anticolisao-posicionamento (nth 62 resultado))
        )
        (setq tentativas-rotacao-original-mantida
          (+ tentativas-rotacao-original-mantida (nth 63 resultado))
        )
        (if (> (nth 64 resultado) 0)
          (setq rotacoes-texto-confirmadas
            (+ rotacoes-texto-confirmadas (nth 0 resultado))
          )
          (if (and
                (eq (nth 53 resultado) 'CURVA_E_ROTACAO_OK)
                (> (nth 0 resultado) 0))
            (setq rotacoes-texto-divergentes
              (+ rotacoes-texto-divergentes (nth 0 resultado))
            )
          )
        )
        (if (> (nth 65 resultado) 0)
          (setq fallbacks-visuais (1+ fallbacks-visuais))
        )
        (setq amostra-orientacao
          (geoauto-adicionar-amostra-orientacao
            registro resultado amostra-orientacao 5
          )
        )
        (if (> (nth 7 resultado) 0)
          (progn
            (setq registros-associados-distancia
              (1+ registros-associados-distancia)
            )
            (setq associados-por-rede
              (geoauto-contador-incrementar
                (nth 4 registro) associados-por-rede
              )
            )
          )
        )
        (setq faixa-associacao (nth 29 resultado))
        (cond
          ((eq faixa-associacao 'PRIMARIO)
            (setq associados-raio-primario
              (+ associados-raio-primario (nth 7 resultado))
            )
          )
          ((eq faixa-associacao 'SECUNDARIO)
            (setq associados-raio-secundario
              (+ associados-raio-secundario (nth 7 resultado))
            )
            (setq amostra-raio-secundario
              (geoauto-adicionar-amostra-associacao
                registro resultado amostra-raio-secundario 5
              )
            )
          )
          ((eq faixa-associacao 'MAXIMO)
            (setq associados-raio-maximo
              (+ associados-raio-maximo (nth 7 resultado))
            )
            (setq amostra-raio-maximo
              (geoauto-adicionar-amostra-associacao
                registro resultado amostra-raio-maximo 5
              )
            )
          )
        )
        (setq valor-ambiguidade (nth 30 resultado))
        (if (> valor-ambiguidade 0)
          (progn
            (setq associacoes-ambiguas (+ associacoes-ambiguas valor-ambiguidade))
            (setq amostra-ambiguas
              (geoauto-adicionar-amostra-associacao
                registro resultado amostra-ambiguas 5
              )
            )
          )
        )
        (if (not (eq (nth 33 resultado) 'OK))
          (setq amostra-sem-associacao
            (geoauto-adicionar-amostra-associacao
              registro resultado amostra-sem-associacao 5
            )
          )
        )
        (if (or (> (nth 2 resultado) 0) (> (nth 3 resultado) 0))
          (setq registros-sem-curva-limite
            (1+ registros-sem-curva-limite)
          )
        )
        (if (> (nth 4 resultado) 0)
          (setq registros-rotacao-padrao
            (1+ registros-rotacao-padrao)
          )
        )

        ; Diagnóstico dirigido aos registros de água potável. A curva global
        ; próxima é apenas explicativa e nunca orienta o texto.
        (if (geoauto-rede-agua-p (nth 4 registro))
          (progn
            (if (eq (nth 42 resultado) 'SUFIXO_XREF)
              (setq agua-layer-xref-prefixado
                (1+ agua-layer-xref-prefixado)
              )
            )
            (if (> (nth 1 resultado) 0)
              (progn
                (setq agua-rotacionada (1+ agua-rotacionada))
                (setq amostra-agua-rotacionada
                  (geoauto-adicionar-amostra-agua
                    registro resultado amostra-agua-rotacionada 3
                  )
                )
              )
              (progn
                (setq motivo-agua
                  (if (nth 34 resultado)
                    (nth 34 resultado)
                    'ERRO_DESCONHECIDO
                  )
                )
                (setq motivos-fallback-agua
                  (geoauto-contador-incrementar
                    (vl-princ-to-string motivo-agua) motivos-fallback-agua
                  )
                )
                (setq amostra-agua-fallback
                  (geoauto-adicionar-amostra-agua
                    registro resultado amostra-agua-fallback
                    *geoauto-limite-amostra-agua*
                  )
                )
                (cond
                  ((eq motivo-agua 'CURVA_FORA_DE_1M)
                    (setq agua-fora-raio (1+ agua-fora-raio))
                  )
                  ((member motivo-agua
                     '(ERRO_PARAMETRO_CURVA ERRO_DERIVADA
                       ERRO_AMOSTRAGEM_TANGENTE ERRO_TANGENTE))
                    (setq agua-erros-tangente (1+ agua-erros-tangente))
                  )
                  ((member motivo-agua
                     '(LAYER_NAO_MAPEADO LAYER_PREFIXADO_POR_XREF))
                    (setq agua-layer-nao-reconhecido
                      (1+ agua-layer-nao-reconhecido)
                    )
                    (setq agua-sem-curva-permitida
                      (1+ agua-sem-curva-permitida)
                    )
                  )
                  ((member motivo-agua '(GEOMETRIA_EM_XREF GEOMETRIA_EM_BLOCO))
                    (setq agua-referencia-aninhada
                      (1+ agua-referencia-aninhada)
                    )
                    (setq agua-sem-curva-permitida
                      (1+ agua-sem-curva-permitida)
                    )
                  )
                  (T
                    (setq agua-sem-curva-permitida
                      (1+ agua-sem-curva-permitida)
                    )
                  )
                )
              )
            )
          )
        )

        (setq pluviais-rotacionados
          (+ pluviais-rotacionados (nth 9 resultado))
        )
        (setq pluviais-sem-curva
          (+ pluviais-sem-curva (nth 10 resultado))
        )
        (setq sem-entidade-textos
          (+ sem-entidade-textos (nth 11 resultado))
        )
        (setq fora-raio-textos
          (+ fora-raio-textos (nth 12 resultado))
        )
        (setq grupos-processados
          (+ grupos-processados (nth 13 resultado))
        )
        (setq grupos-posicao-original
          (+ grupos-posicao-original (nth 14 resultado))
        )
        (setq grupos-frente (+ grupos-frente (nth 15 resultado)))
        (setq grupos-tras (+ grupos-tras (nth 16 resultado)))
        (setq tentativas-reposicionamento
          (+ tentativas-reposicionamento (nth 17 resultado))
        )
        (setq colisoes-detectadas
          (+ colisoes-detectadas (nth 18 resultado))
        )
        (setq posicionamentos-forcados
          (+ posicionamentos-forcados (nth 19 resultado))
        )
        (setq valor-deslocamento (nth 20 resultado))
        (if (> valor-deslocamento maior-deslocamento)
          (setq maior-deslocamento valor-deslocamento)
        )
        (setq curvas-rejeitadas-distancia
          (+ curvas-rejeitadas-distancia (nth 21 resultado))
        )
        (setq amostra-distancias-curvas
          (geoauto-adicionar-amostra-distancia
            (nth 22 resultado) amostra-distancias-curvas 5
          )
        )
        (setq grupos-afastados-perpendicular
          (+ grupos-afastados-perpendicular (nth 23 resultado))
        )
        (setq tentativas-perpendiculares
          (+ tentativas-perpendiculares (nth 24 resultado))
        )
        (setq interferencias-curvas
          (+ interferencias-curvas (nth 25 resultado))
        )
        (setq valor-afastamento (nth 26 resultado))
        (if (> valor-afastamento maior-afastamento-adicional)
          (setq maior-afastamento-adicional valor-afastamento)
        )
        (setq posicionamentos-forcados-curva
          (+ posicionamentos-forcados-curva (nth 27 resultado))
        )
        (setq valor-fallback-bbox (nth 28 resultado))
        (setq fallbacks-bbox
          (+ fallbacks-bbox valor-fallback-bbox)
        )
      )
    )
  )

    )
  )

  ; As definições são verificadas por tblsearch. A primeira inserção usa
  ; o caminho do DWG; as seguintes reutilizam o nome já carregado.
  (setq *geoauto-fase-atual* "INSERCAO_ANOTACOES_DIVISA")
  (if geracao-permitida
    (progn
      (geoauto-debug
        (strcat
          "Iniciando anotacoes de divisa. Registros: "
          (itoa (length *geoauto-anotacoes-divisa*))
        )
      )
      (foreach anotacao-divisa *geoauto-anotacoes-divisa*
        (setq total-textos (1+ total-textos))
        (setq resultado-anotacao-divisa
          (vl-catch-all-apply
            'geoauto-inserir-anotacao-divisa
            (list
              anotacao-divisa
              estilo-anotacao-divisa-ativo
              *geoauto-altura-texto*
            )
          )
        )
        (if (or
              (vl-catch-all-error-p resultado-anotacao-divisa)
              (null resultado-anotacao-divisa))
          (progn
            (setq falhas-anotacoes-divisa
              (1+ falhas-anotacoes-divisa)
            )
            (setq erros-insercao (1+ erros-insercao))
          )
          (progn
            (setq anotacoes-divisa-inseridas
              (1+ anotacoes-divisa-inseridas)
            )
            (setq inseridos (1+ inseridos))
          )
        )
      )
      (setq *geoauto-registro-id-atual* nil)
      (setq *geoauto-codigo-atual* nil)
      (setq *geoauto-ponto-original-atual* nil)
    )
  )

  (setq *geoauto-fase-atual* "INSERCAO_ALERTAS_DIAGNOSTICO")
  (if geracao-permitida
    (foreach alerta-codigo-desconhecido
             *geoauto-alertas-codigos-desconhecidos*
      (setq resultado-atencao
        (vl-catch-all-apply
          'geoauto-inserir-circulo-atencao
          (list
            space
            (nth 1 alerta-codigo-desconhecido)
            (nth 0 alerta-codigo-desconhecido)
            (nth 3 alerta-codigo-desconhecido)
            (nth 2 alerta-codigo-desconhecido)
          )
        )
      )
      (cond
        ((eq resultado-atencao 'SOBREPOSICAO)
          (setq circulos-atencao-sobrepostos-ignorados
            (1+ circulos-atencao-sobrepostos-ignorados)
          )
        )
        ((or
              (vl-catch-all-error-p resultado-atencao)
              (null resultado-atencao))
          (setq falhas-alertas-diagnostico
            (1+ falhas-alertas-diagnostico)
          )
          (setq falhas-circulos-atencao
            (1+ falhas-circulos-atencao)
          )
        )
        (T
          (setq alertas-diagnostico-inseridos
            (1+ alertas-diagnostico-inseridos)
          )
          (setq circulos-atencao-inseridos
            (1+ circulos-atencao-inseridos)
          )
        )
      )
    )
  )

  (setq *geoauto-fase-atual* "INSERCAO_ESTRUTURAS_PLUVIAIS")
  (if geracao-permitida
    (progn
      (foreach estrutura-pluvial *geoauto-estruturas-pluviais*
        (setq resultado-estrutura-pluvial
          (if
            (geoauto-ponto-ocupado-por-tipo-p
              (nth 0 estrutura-pluvial)
              (nth 3 estrutura-pluvial)
              estruturas-pluviais-ocupadas
              *geoauto-tolerancia-sobreposicao-blocos*
            )
            'SOBREPOSICAO
            (vl-catch-all-apply
              'geoauto-inserir-estrutura-pluvial
              (list space estrutura-pluvial)
            )
          )
        )
        (cond
          ((eq resultado-estrutura-pluvial 'SOBREPOSICAO)
            (setq estruturas-pluviais-sobrepostas-ignoradas
              (1+ estruturas-pluviais-sobrepostas-ignoradas)
            )
          )
          ((or
                (vl-catch-all-error-p resultado-estrutura-pluvial)
                (null resultado-estrutura-pluvial))
            (progn
              (setq falhas-estruturas-pluviais
                (1+ falhas-estruturas-pluviais)
              )
              (if (= (nth 0 estrutura-pluvial) "GRELHA")
                (setq falhas-grelhas (1+ falhas-grelhas))
                (setq falhas-bocas-lobo (1+ falhas-bocas-lobo))
              )
            )
          )
          (T
            (setq estruturas-pluviais-inseridas
              (1+ estruturas-pluviais-inseridas)
            )
            (setq estruturas-pluviais-ocupadas
              (cons
                (list
                  (nth 0 estrutura-pluvial)
                  (nth 3 estrutura-pluvial)
                )
                estruturas-pluviais-ocupadas
              )
            )
            (if (= (nth 0 estrutura-pluvial) "GRELHA")
              (setq grelhas-inseridas (1+ grelhas-inseridas))
              (setq bocas-lobo-inseridas (1+ bocas-lobo-inseridas))
            )
          )
        )
      )
      (foreach alerta-estrutura-pluvial
               *geoauto-alertas-estruturas-pluviais*
        (setq resultado-atencao
          (vl-catch-all-apply
            'geoauto-inserir-circulo-atencao
            (list
              space
              (nth 2 alerta-estrutura-pluvial)
              (nth 1 alerta-estrutura-pluvial)
              (nth 4 alerta-estrutura-pluvial)
              (nth 3 alerta-estrutura-pluvial)
            )
          )
        )
        (cond
          ((eq resultado-atencao 'SOBREPOSICAO)
            (setq circulos-atencao-sobrepostos-ignorados
              (1+ circulos-atencao-sobrepostos-ignorados)
            )
          )
          ((or
                (vl-catch-all-error-p resultado-atencao)
                (null resultado-atencao))
            (setq falhas-alertas-estruturas-pluviais
              (1+ falhas-alertas-estruturas-pluviais)
            )
            (setq falhas-circulos-atencao
              (1+ falhas-circulos-atencao)
            )
          )
          (T
            (setq alertas-estruturas-pluviais-inseridos
              (1+ alertas-estruturas-pluviais-inseridos)
            )
            (setq circulos-atencao-inseridos
              (1+ circulos-atencao-inseridos)
            )
          )
        )
      )
    )
  )

  (setq *geoauto-fase-atual* "INSERCAO_BLOCOS")
  (if (and geracao-permitida *geoauto-habilitar-blocos*)
    (progn
      (geoauto-debug
        (strcat "Iniciando blocos. Instruções: "
                (itoa (length *geoauto-instrucoes-blocos*)))
      )
      (foreach instrucao-bloco *geoauto-instrucoes-blocos*
        (setq block-key (nth 0 instrucao-bloco))
        (setq resultado-bloco
          (vl-catch-all-apply
            'geo:insert-block
            (list
              space instrucao-bloco curvas-por-layer
              *geoauto-raio-primario* *geoauto-raio-secundario*
              *geoauto-raio-maximo* *geoauto-tolerancia-ambiguidade*
              *geoauto-amostra-tangente*
              pontos-blocos-ocupados
              *geoauto-tolerancia-sobreposicao-blocos*
            )
          )
        )
        (if (vl-catch-all-error-p resultado-bloco)
          (setq falhas-blocos
            (geoauto-contador-incrementar block-key falhas-blocos)
          )
          (progn
            (setq status-bloco (nth 0 resultado-bloco))
            (setq curvas-rejeitadas-distancia
              (+ curvas-rejeitadas-distancia (nth 5 resultado-bloco))
            )
            (setq amostra-distancias-curvas
              (geoauto-adicionar-amostra-distancia
                (nth 6 resultado-bloco) amostra-distancias-curvas 5
              )
            )
            (cond
              ((eq status-bloco 'SOBREPOSICAO)
                (setq blocos-sobrepostos-ignorados
                  (1+ blocos-sobrepostos-ignorados)
                )
              )
              ((eq status-bloco 'OK)
                (setq blocos-inseridos (1+ blocos-inseridos))
                (setq pontos-blocos-ocupados
                  (cons
                    (list
                      (geoauto-chave-familia-bloco
                        (nth 1 instrucao-bloco)
                      )
                      (nth 4 instrucao-bloco)
                    )
                    pontos-blocos-ocupados
                  )
                )
                (setq blocos-por-tipo
                  (geoauto-contador-incrementar block-key blocos-por-tipo)
                )
                (if (= (strcase (nth 10 instrucao-bloco)) "POSSIVEL_PV")
                  (progn
                    (setq resultado-atencao
                      (vl-catch-all-apply
                        'geoauto-inserir-circulo-atencao
                        (list
                          space
                          (nth 4 instrucao-bloco)
                          (nth 3 instrucao-bloco)
                          (nth 14 instrucao-bloco)
                          (nth 11 instrucao-bloco)
                        )
                      )
                    )
                    (cond
                      ((eq resultado-atencao 'SOBREPOSICAO)
                        (setq circulos-atencao-sobrepostos-ignorados
                          (1+ circulos-atencao-sobrepostos-ignorados)
                        )
                      )
                      ((or
                            (vl-catch-all-error-p resultado-atencao)
                            (null resultado-atencao))
                        (setq falhas-circulos-atencao
                          (1+ falhas-circulos-atencao)
                        )
                      )
                      (T
                        (setq circulos-atencao-inseridos
                          (1+ circulos-atencao-inseridos)
                        )
                      )
                    )
                  )
                )
                (if (nth 2 resultado-bloco)
                  (setq blocos-rotacao-rede (1+ blocos-rotacao-rede))
                )
                (if (nth 3 resultado-bloco)
                  (setq blocos-rotacao-fallback
                    (1+ blocos-rotacao-fallback)
                  )
                )
              )
              ((eq status-bloco 'ARQUIVO-AUSENTE)
                (setq arquivos-ausentes-runtime
                  (geoauto-contador-incrementar
                    (geoauto-nome-arquivo (nth 4 resultado-bloco))
                    arquivos-ausentes-runtime
                  )
                )
              )
              (T
                (setq falhas-blocos
                  (geoauto-contador-incrementar block-key falhas-blocos)
                )
              )
            )
          )
        )
      )

    )
    (geoauto-debug "Inserção de blocos desativada por configuração.")
  )

  (setq *geoauto-fase-atual* "FINALIZACAO")
  (vl-catch-all-apply 'vla-Regen (list doc 1))
  (if undo-open
    (progn
      (vl-catch-all-apply 'geoauto-finalizar-undo (list doc))
      (setq undo-open nil)
    )
  )

  (princ
    (strcat
      "\nObjetos anteriores desta importação encontrados: "
      (itoa objetos-antigos-encontrados)
    )
  )
  (princ
    (strcat
      "\nObjetos anteriores desta importação removidos ou substituídos: "
      (itoa objetos-antigos-removidos)
    )
  )
  (if (> falhas-remocao-objetos 0)
    (princ
      (strcat
        "\nFalhas ao remover objetos anteriores desta importação: "
        (itoa falhas-remocao-objetos)
      )
    )
  )
  (princ
    (strcat
      "\nTextos originais do desenho ignorados pela colisão: "
      (itoa textos-usuario-ignorados)
    )
  )
  (princ
    (strcat
      "\nExecução duplicada evitada: "
      (itoa execucao-duplicada-evitada)
    )
  )
  (if (not geracao-permitida)
    (princ
      "\nNova inserção bloqueada porque a limpeza segura não foi concluída."
    )
  )

  (princ "\nGeoAutoEngine concluido.")
  (princ (strcat "\nTextos processados: " (itoa total-textos)))
  (princ (strcat "\nTextos inseridos: " (itoa inseridos)))
  (princ
    (strcat
      "\nTextos associados a curvas abertas: "
      (itoa associados-abertas)
    )
  )
  (princ
    (strcat
      "\nTextos rotacionados pela tubulacao: "
      (itoa rotacionados)
    )
  )
  (princ
    (strcat
      "\nTextos com curva e rotação resolvida: "
      (itoa rotacionados)
    )
  )
  (princ
    (strcat
      "\nTextos com curva, mas erro de tangente: "
      (itoa erros-tangente)
    )
  )
  (princ
    (strcat
      "\nRotações preservadas após fallback visual: "
      (itoa
        (+ rotacoes-preservadas-fallback-visual
           rotacoes-preservadas-fallback-externo)
      )
    )
  )
  (princ
    (strcat
      "\nFalhas no bounding box: "
      (itoa falhas-bbox-posicionamento)
    )
  )
  (princ
    (strcat
      "\nFalhas na anticolisão: "
      (itoa falhas-anticolisao-posicionamento)
    )
  )
  (princ
    (strcat
      "\nReposicionamentos longitudinais: "
      (itoa (+ grupos-frente grupos-tras))
    )
  )
  (princ
    (strcat
      "\nTentativas com rotação original mantida: "
      (itoa tentativas-rotacao-original-mantida)
    )
  )
  (princ
    (strcat
      "\nRotation confirmada igual ao angulo_texto_final: "
      (itoa rotacoes-texto-confirmadas)
    )
  )
  (princ
    (strcat
      "\nDivergências entre Rotation e angulo_texto_final: "
      (itoa rotacoes-texto-divergentes)
    )
  )
  (princ
    (strcat
      "\nFallbacks visuais aplicados: "
      (itoa fallbacks-visuais)
    )
  )
  (princ
    (strcat
      "\nCurvas abertas coletadas: "
      (itoa curvas-abertas-indexadas)
    )
  )
  (princ
    (strcat
      "\nCurvas fechadas ignoradas: "
      (itoa curvas-fechadas-ignoradas)
    )
  )
  (princ
    (strcat
      "\nRegistros associados por distância: "
      (itoa registros-associados-distancia)
    )
  )
  (princ
    (strcat
      "\nTextos associados até 0,10 m: "
      (itoa associados-raio-primario)
    )
  )
  (princ
    (strcat
      "\nTextos associados entre 0,10 m e 0,50 m: "
      (itoa associados-raio-secundario)
    )
  )
  (princ
    (strcat
      "\nTextos associados entre 0,50 m e 0,70 m: "
      (itoa associados-raio-maximo)
    )
  )
  (princ
    (strcat
      "\nRegistros sem tubulação válida até 0,70 m: "
      (itoa registros-sem-curva-limite)
    )
  )
  (princ
    (strcat
      "\nAssociações geométricas ambíguas: "
      (itoa associacoes-ambiguas)
    )
  )
  (princ
    (strcat
      "\nRegistros ignorados sem orientação resolvida: "
      (itoa registros-rotacao-padrao)
    )
  )
  (princ
    (strcat
      "\nRegistros sem curva válida nos layers permitidos: "
      (itoa sem-entidade)
    )
  )
  (princ
    (strcat
      "\nTextos afetados por ausência de curva válida: "
      (itoa sem-entidade-textos)
    )
  )
  (princ
    (strcat
      "\nRegistros com tubulação reconhecida fora de 0,70 m: "
      (itoa fora-raio)
    )
  )
  (princ
    (strcat
      "\nTextos afetados por geometria fora do raio: "
      (itoa fora-raio-textos)
    )
  )
  (princ
    (strcat
      "\nTextos encaminhados à contingência ou ignorados: "
      (itoa fallbacks)
    )
  )
  (princ
    (strcat
      "\nTextos inseridos sem orientação resolvida (deve ser 0): "
      (itoa rotacao-padrao)
    )
  )
  (princ
    (strcat
      "\nTextos pluviais rotacionados: "
      (itoa pluviais-rotacionados)
    )
  )
  (princ
    (strcat
      "\nTextos pluviais sem curva válida: "
      (itoa pluviais-sem-curva)
    )
  )
  (princ
    (strcat
      "\nErros ao calcular tangente: "
      (itoa erros-tangente)
    )
  )
  (princ
    (strcat "\nÁgua potável processada: " (itoa agua-processada))
  )
  (princ
    (strcat "\nÁgua potável rotacionada: " (itoa agua-rotacionada))
  )
  (princ
    (strcat
      "\nÁgua potável sem curva permitida: "
      (itoa agua-sem-curva-permitida)
    )
  )
  (princ
    (strcat "\nÁgua potável fora do raio: " (itoa agua-fora-raio))
  )
  (princ
    (strcat
      "\nÁgua potável com erro de tangente: "
      (itoa agua-erros-tangente)
    )
  )
  (princ
    (strcat
      "\nÁgua potável em layer não reconhecido: "
      (itoa agua-layer-nao-reconhecido)
    )
  )
  (princ
    (strcat
      "\nÁgua potável possivelmente dentro de XREF ou bloco: "
      (itoa agua-referencia-aninhada)
    )
  )
  (princ
    (strcat
      "\nÁgua potável associada por sufixo de layer de XREF: "
      (itoa agua-layer-xref-prefixado)
    )
  )
  (geoauto-imprimir-contadores
    "Motivos de fallback da água potável:" motivos-fallback-agua
  )
  (princ
    (strcat
      "\nErros ao inserir texto: "
      (itoa erros-insercao)
    )
  )
  (princ
    (strcat
      "\nAnotacoes de divisa inseridas: "
      (itoa anotacoes-divisa-inseridas)
    )
  )
  (if (> falhas-anotacoes-divisa 0)
    (princ
      (strcat
        "\nFalhas ao inserir anotacoes de divisa: "
        (itoa falhas-anotacoes-divisa)
      )
    )
  )
  (if (> fallbacks-bbox 0)
    (princ
      (strcat
        "\nGrupos mantidos sem BoundingBox: "
        (itoa fallbacks-bbox)
      )
    )
  )

  (setq textos-sem-curva-limite
    (+ sem-entidade-textos fora-raio-textos)
  )
  (if (> textos-sem-curva-limite 0)
    (princ
      (strcat
        "\nTextos sem tubulação válida até 0,70 m: "
        (itoa textos-sem-curva-limite)
      )
    )
  )
  (if (> curvas-rejeitadas-distancia 0)
    (princ
      (strcat
        "\nTubulações reconhecidas rejeitadas por distância (> 0,70 m): "
        (itoa curvas-rejeitadas-distancia)
      )
    )
  )
  (geoauto-imprimir-contadores
    "Registros processados por rede:" registros-por-rede
  )
  (geoauto-imprimir-contadores
    "Registros associados por rede:" associados-por-rede
  )
  (geoauto-imprimir-amostra-distancias amostra-distancias-curvas)
  (geoauto-imprimir-amostra-associacoes
    "Amostra sem associação até 0,70 m:"
    amostra-sem-associacao
  )
  (geoauto-imprimir-amostra-associacoes
    "Amostra associada no raio secundário:"
    amostra-raio-secundario
  )
  (geoauto-imprimir-amostra-associacoes
    "Amostra associada no raio máximo:"
    amostra-raio-maximo
  )
  (geoauto-imprimir-amostra-associacoes
    "Amostra de associações ambíguas:"
    amostra-ambiguas
  )

  (geoauto-imprimir-amostra-agua
    "Comparação — água potável rotacionada (até 3 registros):"
    amostra-agua-rotacionada
  )
  (geoauto-imprimir-amostra-agua
    "Diagnóstico — água potável sem orientação e sem inserção (até 10 registros):"
    amostra-agua-fallback
  )
  (geoauto-imprimir-amostra-orientacao
    "Amostra de orientação imutável (até 5 registros):"
    amostra-orientacao
  )

  ; Diagnóstico de colisão: contadores nulos não poluem o prompt.
  (if (> obstaculos-pv-registrados 0)
    (princ
      (strcat
        "\nPVs protegidos contra sobreposição de textos: "
        (itoa obstaculos-pv-registrados)
      )
    )
  )
  (princ
    (strcat
      "\nGrupos de texto processados: "
      (itoa grupos-processados)
    )
  )
  (if (> grupos-posicao-original 0)
    (princ
      (strcat
        "\nGrupos inseridos na posição original: "
        (itoa grupos-posicao-original)
      )
    )
  )
  (setq reposicionados-total (+ grupos-frente grupos-tras))
  (if (> reposicionados-total 0)
    (princ
      (strcat
        "\nGrupos reposicionados automaticamente: "
        (itoa reposicionados-total)
      )
    )
  )
  (if (> grupos-frente 0)
    (princ
      (strcat
        "\nGrupos reposicionados para frente: "
        (itoa grupos-frente)
      )
    )
  )
  (if (> grupos-tras 0)
    (princ
      (strcat
        "\nGrupos reposicionados para trás: "
        (itoa grupos-tras)
      )
    )
  )
  (if (> tentativas-reposicionamento 0)
    (princ
      (strcat
        "\nTentativas de reposicionamento: "
        (itoa tentativas-reposicionamento)
      )
    )
  )
  (if (> colisoes-detectadas 0)
    (princ
      (strcat
        "\nColisões de texto detectadas: "
        (itoa colisoes-detectadas)
      )
    )
  )
  (if (> posicionamentos-forcados 0)
    (princ
      (strcat
        "\nPosicionamentos forçados: "
        (itoa posicionamentos-forcados)
      )
    )
  )
  (if (> maior-deslocamento 0.0)
    (princ
      (strcat
        "\nMaior deslocamento longitudinal aplicado: "
        (rtos maior-deslocamento 2 3)
      )
    )
  )
  (if (> textos-existentes-considerados 0)
    (princ
      (strcat
        "\nTextos existentes considerados: "
        (itoa textos-existentes-considerados)
      )
    )
  )

  ; Diagnóstico da proteção entre textos e curvas.
  (if (> grupos-afastados-perpendicular 0)
    (princ
      (strcat
        "\nGrupos afastados perpendicularmente da tubulação: "
        (itoa grupos-afastados-perpendicular)
      )
    )
  )
  (if (> tentativas-perpendiculares 0)
    (princ
      (strcat
        "\nTentativas de afastamento perpendicular: "
        (itoa tentativas-perpendiculares)
      )
    )
  )
  (if (> interferencias-curvas 0)
    (princ
      (strcat
        "\nInterferências entre textos e polylines detectadas: "
        (itoa interferencias-curvas)
      )
    )
  )
  (if (> maior-afastamento-adicional 0.0)
    (princ
      (strcat
        "\nMaior afastamento perpendicular adicional: "
        (rtos maior-afastamento-adicional 2 3)
      )
    )
  )
  (if (> posicionamentos-forcados-curva 0)
    (princ
      (strcat
        "\nGrupos forçados ainda próximos de polyline: "
        (itoa posicionamentos-forcados-curva)
      )
    )
  )

  (princ
    (strcat
      "\nBlockReferences comuns encontrados no espaço atual: "
      (itoa blocos-referenciados)
    )
  )
  (princ
    (strcat
      "\nXREFs encontrados no espaço atual: "
      (itoa xrefs-referenciadas)
    )
  )
  (geoauto-imprimir-tabela-curvas
    "Tabela de curvas abertas por layer:"
    tabela-curvas-abertas *geoauto-layers-agua* 50
  )
  (geoauto-imprimir-tabela-curvas
    "Tabela de curvas fechadas ignoradas por layer:"
    tabela-curvas-fechadas *geoauto-layers-agua* 50
  )
  (geoauto-imprimir-tabela-curvas
    "Tabela de entidades não suportadas por layer (diagnóstico):"
    tabela-entidades-nao-suportadas *geoauto-layers-agua* 50
  )
  (geoauto-imprimir-layers
    "Layers contendo curvas abertas coletadas:"
    layers-abertos
    25
  )
  (geoauto-imprimir-layers
    "Layers contendo curvas fechadas ignoradas:"
    layers-fechados
    25
  )
  (geoauto-imprimir-layers
    "Layers de curvas existentes no desenho (amostra):"
    layers-curvas-desenho
    25
  )
  (if (> blocos-inseridos 0)
    (princ
      (strcat "\nBlocos inseridos: " (itoa blocos-inseridos))
    )
  )
  (geoauto-imprimir-contadores
    "Blocos inseridos por tipo:" blocos-por-tipo
  )
  (geoauto-imprimir-contadores
    "Arquivos de bloco ausentes na geração:"
    *geoauto-blocos-arquivos-ausentes*
  )
  (geoauto-imprimir-contadores
    "Arquivos de bloco ausentes na execução:"
    arquivos-ausentes-runtime
  )
  (geoauto-imprimir-contadores
    "Elementos sem mapeamento seguro de bloco:"
    *geoauto-blocos-sem-mapeamento*
  )
  (geoauto-imprimir-contadores
    "Coordenadas inválidas para blocos:"
    *geoauto-blocos-coordenadas-invalidas*
  )
  (geoauto-imprimir-contadores
    "Duplicatas exatas de blocos ignoradas:"
    *geoauto-blocos-duplicatas-ignoradas*
  )
  (geoauto-imprimir-contadores
    "Blocos próximos da mesma família ignorados na geração:"
    *geoauto-blocos-proximidade-ignorados*
  )
  (geoauto-imprimir-contadores
    "Falhas de inserção de blocos:" falhas-blocos
  )
  (princ
    (strcat
      "\nGrelhas e bocas de lobo inseridas: "
      (itoa estruturas-pluviais-inseridas)
    )
  )
  (princ
    (strcat
      "\nGrelhas inseridas: "
      (itoa grelhas-inseridas)
    )
  )
  (princ
    (strcat
      "\nBocas de lobo inseridas: "
      (itoa bocas-lobo-inseridas)
    )
  )
  (if (> falhas-grelhas 0)
    (princ
      (strcat
        "\nFalhas ao inserir grelhas: "
        (itoa falhas-grelhas)
      )
    )
  )
  (if (> falhas-bocas-lobo 0)
    (princ
      (strcat
        "\nFalhas ao inserir bocas de lobo: "
        (itoa falhas-bocas-lobo)
      )
    )
  )
  (if (> falhas-estruturas-pluviais 0)
    (princ
      (strcat
        "\nFalhas ao inserir grelhas e bocas de lobo: "
        (itoa falhas-estruturas-pluviais)
      )
    )
  )
  (if (> estruturas-pluviais-sobrepostas-ignoradas 0)
    (princ
      (strcat
        "\nGrelhas e bocas de lobo próximas ignoradas: "
        (itoa estruturas-pluviais-sobrepostas-ignoradas)
      )
    )
  )
  (if (> alertas-estruturas-pluviais-inseridos 0)
    (princ
      (strcat
        "\nEstruturas pluviais marcadas para revisao: "
        (itoa alertas-estruturas-pluviais-inseridos)
      )
    )
  )
  (if (> falhas-alertas-estruturas-pluviais 0)
    (princ
      (strcat
        "\nFalhas nos alertas de estruturas pluviais: "
        (itoa falhas-alertas-estruturas-pluviais)
      )
    )
  )
  (if (> blocos-sobrepostos-ignorados 0)
    (princ
      (strcat
        "\nBlocos ignorados por sobreposicao: "
        (itoa blocos-sobrepostos-ignorados)
      )
    )
  )
  (if (> alertas-diagnostico-inseridos 0)
    (princ
      (strcat
        "\nPontos desconhecidos destacados no desenho: "
        (itoa alertas-diagnostico-inseridos)
      )
    )
  )
  (if (> falhas-alertas-diagnostico 0)
    (princ
      (strcat
        "\nFalhas ao destacar pontos desconhecidos: "
        (itoa falhas-alertas-diagnostico)
      )
    )
  )
  (if (> circulos-atencao-inseridos 0)
    (princ
      (strcat
        "\nCírculos vermelhos de atenção inseridos: "
        (itoa circulos-atencao-inseridos)
      )
    )
  )
  (if (> circulos-atencao-sobrepostos-ignorados 0)
    (princ
      (strcat
        "\nCírculos de atenção próximos ignorados: "
        (itoa circulos-atencao-sobrepostos-ignorados)
      )
    )
  )
  (if (> falhas-circulos-atencao 0)
    (princ
      (strcat
        "\nFalhas ao inserir círculos de atenção: "
        (itoa falhas-circulos-atencao)
      )
    )
  )
  (if (> blocos-rotacao-rede 0)
    (princ
      (strcat
        "\nBlocos com rotação pela rede: "
        (itoa blocos-rotacao-rede)
      )
    )
  )
  (if (> blocos-rotacao-fallback 0)
    (princ
      (strcat
        "\nBlocos com rotação fallback: "
        (itoa blocos-rotacao-fallback)
      )
    )
  )
  (princ
    (strcat
      "\nLimite longitudinal configurado: "
      (rtos *geoauto-max-deslocamento-longitudinal* 2 2)
      " m"
    )
  )
  (princ
    (strcat
      "\nLimite perpendicular adicional configurado: "
      (rtos *geoauto-max-afastamento-perpendicular-adicional* 2 2)
      " m"
    )
  )
  (if (> maior-deslocamento (+ *geoauto-max-deslocamento-longitudinal* 1.0e-9))
    (princ "\nALERTA: limite longitudinal excedido.")
    (princ
      (strcat
        "\nConfirmação: maior deslocamento longitudinal <= "
        (rtos *geoauto-max-deslocamento-longitudinal* 2 2)
        " m."
      )
    )
  )
  (if (> maior-afastamento-adicional
         (+ *geoauto-max-afastamento-perpendicular-adicional* 1.0e-9))
    (princ "\nALERTA: limite perpendicular excedido.")
    (princ
      (strcat
        "\nConfirmação: maior afastamento perpendicular adicional <= "
        (rtos *geoauto-max-afastamento-perpendicular-adicional* 2 2)
        " m."
      )
    )
  )

  (setq *geoauto-fase-atual* "CONCLUIDO")
  (geoauto-debug "Rotina finalizada.")
  (princ)
)

(defun c:GEOAUTOENGINE ()
  (c:GEOAUTOPROF)
)

(c:GEOAUTOPROF)
(princ)
