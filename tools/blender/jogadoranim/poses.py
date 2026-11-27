# Poses-chave dos dribles/arrancadas do jogador (esqueleto Mixamo do Jogador.fbx, olhando para -Y).
# Mesmas convenções do kit do goleiro (tools/blender/gkanim/posekit.py):
#   leg(lado, coxa, joelho, pé, abertura): coxa + = para a frente; joelho + = dobra; pé + = ponta para cima;
#   abertura + = perna para fora.  arm(lado, ergue, frente, cotovelo): ergue - = braço para baixo a partir da pose T.
#   spine(inclinação + = para a frente, lado + = para a direita, giro).  _H = deslocamento do quadril (direita, frente, cima) em m.
# Todas as jogadas vão para a DIREITA do jogador; build.py gera a versão espelhada (Esq).
def leg(side, thigh, knee, foot, spread=4, twist=0):
    s = 1 if side == 'Left' else -1
    return {side+'UpLeg': [('R', thigh), ('F', spread*s), ('U', twist*s)], side+'Leg': [('R', -knee)], side+'Foot': [('R', foot)]}
def arm(side, up, fwd, elbow, shoulder=0, hand=0, twist=0):
    s = 1 if side == 'Left' else -1
    return {side+'Shoulder': [('F', shoulder*s)], side+'Arm': [('F', up*s), ('U', -fwd*s), ('R', twist)],
            side+'ForeArm': [('U', -elbow*s)], side+'Hand': [('R', hand)]}
def spine(lean, side=0, twist=0, neck=0, head=0):
    return {'Spine': [('R', -lean*0.45), ('F', side*0.4), ('U', twist*0.4)], 'Spine1': [('R', -lean*0.35), ('F', side*0.35), ('U', twist*0.35)],
            'Spine2': [('R', -lean*0.2), ('F', side*0.25), ('U', twist*0.25)], 'Neck': [('R', neck)], 'Head': [('R', head)]}
def merge(*ds):
    out = {}
    for d in ds:
        for k, v in d.items(): out.setdefault(k, []).extend(v)
    return out

# ---- postura base de condução (joelhos levemente dobrados, tronco à frente, braços soltos)
BASE = merge(leg('Left', 22, 34, 10, 5), leg('Right', 22, 34, 10, 5), arm('Left', -68, 18, 55), arm('Right', -68, 18, 55),
             spine(12, neck=4, head=-2))
BASE_H = (0, -0.02, -0.05)

# ---- finta de corpo: ameaça ir para a ESQUERDA e sai pela DIREITA
F_AMEACA = merge(leg('Left', 18, 30, 8, 22), leg('Right', 30, 52, 16, 2), arm('Left', -40, 10, 40), arm('Right', -72, 30, 60),
                 spine(14, side=-14, twist=-10, neck=4, head=-4), {'Hips': [('F', -8)]})
F_AMEACA_H = (-0.13, 0, -0.08)
F_CARGA = merge(leg('Left', 26, 58, 18, 26), leg('Right', 18, 36, 8, 8), arm('Left', -46, 12, 45), arm('Right', -60, 34, 60),
                spine(18, side=-10, twist=-4, neck=6, head=-2), {'Hips': [('F', -10)]})
F_CARGA_H = (-0.15, 0, -0.13)
F_SAIDA = merge(leg('Left', -18, 22, -18, 10), leg('Right', 42, 62, 12, 24), arm('Left', -60, 55, 70), arm('Right', -45, -20, 50),
                spine(22, side=12, twist=12, neck=8, head=0), {'Hips': [('F', 10)]})
F_SAIDA_H = (0.14, 0.03, -0.08)
F_CORRIDA = merge(leg('Left', 8, 40, 0, 6), leg('Right', 34, 48, 12, 10), arm('Left', -62, 40, 70), arm('Right', -62, -25, 60),
                  spine(18, side=4, twist=6, neck=6, head=-2))
F_CORRIDA_H = (0.08, 0.02, -0.06)

# ---- pedalada: a perna ESQUERDA passa por cima da bola (de dentro para fora) e a direita leva a bola para a DIREITA
P_ERGUE = merge(leg('Left', 48, 70, 14, -14, 18), leg('Right', 24, 40, 12, 4), arm('Left', -50, 15, 50), arm('Right', -55, 30, 55),
                spine(14, side=6, twist=-8, neck=6, head=-6))
P_ERGUE_H = (0.03, 0, -0.07)
P_VOLTA = merge(leg('Left', 40, 58, 6, 18, -20), leg('Right', 26, 44, 12, 2), arm('Left', -35, 5, 40), arm('Right', -60, 35, 60),
                spine(14, side=-8, twist=-14, neck=6, head=-6), {'Hips': [('F', -6)]})
P_VOLTA_H = (-0.08, 0, -0.07)
P_APOIO = merge(leg('Left', 16, 36, 12, 26), leg('Right', 26, 50, 14, 6), arm('Left', -42, 10, 45), arm('Right', -66, 32, 60),
                spine(16, side=-12, twist=-8, neck=6, head=-4), {'Hips': [('F', -9)]})
P_APOIO_H = (-0.14, 0, -0.11)
P_LEVA = merge(leg('Left', -12, 26, -14, 10), leg('Right', 36, 52, 4, 26, -16), arm('Left', -58, 50, 65), arm('Right', -42, -15, 50),
               spine(20, side=12, twist=12, neck=8, head=-2), {'Hips': [('F', 9)]})
P_LEVA_H = (0.13, 0.02, -0.08)

# ---- puxada com a sola (pé DIREITO na bola, recua e gira)
U_SOLA = merge(leg('Left', 18, 34, 10, 5), leg('Right', 38, 22, -24, 2), arm('Left', -55, 25, 55), arm('Right', -62, 10, 50),
               spine(6, neck=6, head=-10))
U_SOLA_H = (0, -0.03, -0.05)
U_PUXA = merge(leg('Left', 24, 44, 12, 6), leg('Right', -6, 46, -16, 4), arm('Left', -50, 30, 60), arm('Right', -55, -10, 55),
               spine(10, twist=18, neck=6, head=-8))
U_PUXA_H = (0, -0.08, -0.08)
U_GIRA = merge(leg('Left', 28, 52, 14, 8, 10), leg('Right', 10, 40, 4, 12, -18), arm('Left', -52, 35, 60), arm('Right', -58, 0, 55),
               spine(14, side=6, twist=26, neck=6, head=-4), {'Hips': [('U', 30)]})
U_GIRA_H = (0.03, -0.06, -0.09)

# ---- arrancada explosiva (primeiras passadas)
A_IMPULSO = merge(leg('Left', -22, 18, -30, 4), leg('Right', 72, 96, 22, 4), arm('Left', -40, 70, 85), arm('Right', -55, -45, 60),
                  spine(34, neck=14, head=4))
A_IMPULSO_H = (0, 0.06, -0.08)
A_TROCA = merge(leg('Left', 66, 92, 20, 4), leg('Right', -24, 20, -30, 4), arm('Left', -55, -45, 60), arm('Right', -40, 70, 85),
                spine(30, neck=12, head=4))
A_TROCA_H = (0, 0.06, -0.06)
A_CORRE = merge(leg('Left', -16, 30, -20, 4), leg('Right', 58, 84, 16, 4), arm('Left', -45, 55, 80), arm('Right', -58, -35, 60),
                spine(22, neck=8, head=2))
A_CORRE_H = (0, 0.04, -0.04)

# ---- freada brusca (perna da frente estendida, quadril baixo, tronco para trás)
R_CORRE = merge(leg('Left', -12, 34, -16, 4), leg('Right', 48, 70, 14, 4), arm('Left', -48, 45, 75), arm('Right', -58, -30, 60),
                spine(18, neck=8, head=2))
R_CORRE_H = (0, 0.02, -0.04)
R_FREIA = merge(leg('Left', 2, 78, 6, 6), leg('Right', 52, 10, -18, 6), arm('Left', -52, 40, 50), arm('Right', -50, 30, 45),
                spine(-12, neck=-4, head=6))
R_FREIA_H = (0, -0.10, -0.16)
R_ASSENTA = merge(leg('Left', 30, 62, 18, 7), leg('Right', 34, 48, 12, 7), arm('Left', -55, 25, 55), arm('Right', -55, 25, 55),
                  spine(8, neck=4, head=0))
R_ASSENTA_H = (0, -0.05, -0.12)

# ---- parado (postura atlética natural de campo: pés 100% plantados no chão, só leve respiração no tronco/braços)
IDLE_A = merge(leg('Left', 8, 15, 7, 4.5), leg('Right', 7, 14, 7, 4.5),
               arm('Left', -72, 10, 22), arm('Right', -72, 10, 22),
               spine(4, neck=2, head=-1))
IDLE_A_H = (0, -0.01, -0.014)

IDLE_B = merge(leg('Left', 8, 15, 7, 4.5), leg('Right', 7, 14, 7, 4.5),
               arm('Left', -70.5, 11, 24, shoulder=1.2), arm('Right', -70.5, 11, 24, shoulder=1.2),
               spine(5.2, neck=2.6, head=-1.4))
IDLE_B_H = (0, -0.01, -0.014)

# ---- queda para frente/lado (falta por trás, tropeço ou escorregão: amortece com antebraços e quadril acima da grama)
Q_IMPACTO = merge(leg('Left', 32, 58, 12, 8), leg('Right', -18, 42, -20, 6),
                  arm('Left', -35, 55, 40), arm('Right', -30, 60, 35),
                  spine(36, side=8, twist=10, neck=10, head=-8), {'Hips': [('R', -22), ('F', 10)]})
Q_IMPACTO_H = (0.04, 0.10, -0.18)

Q_DESCIDA = merge(leg('Left', 42, 82, -8, 10), leg('Right', 12, 65, -18, 8),
                  arm('Left', -15, 75, 55), arm('Right', -10, 78, 50),
                  spine(28, side=14, twist=14, neck=6, head=-12), {'Hips': [('R', -58), ('F', 28)]})
Q_DESCIDA_H = (0.10, 0.22, -0.52)

Q_SOLO = merge(leg('Left', 36, 68, -10, 8), leg('Right', 18, 44, -12, 6),
               arm('Left', -25, 68, 72), arm('Right', -35, 58, 65),
               spine(16, side=10, twist=8, neck=8, head=4), {'Hips': [('R', -74), ('F', 52)]})
Q_SOLO_H = (0.14, 0.26, -0.78)

Q_REPOUSO = merge(leg('Left', 30, 56, -8, 7), leg('Right', 14, 36, -10, 5),
                  arm('Left', -32, 62, 78), arm('Right', -40, 52, 70),
                  spine(12, side=8, twist=6, neck=6, head=4), {'Hips': [('R', -76), ('F', 58)]})
Q_REPOUSO_H = (0.14, 0.26, -0.80)

# ---- queda de costas/sentado (trombada de frente: desequilibra para trás e senta apoiando as mãos no gramado)
QC_IMPACTO = merge(leg('Left', 18, 36, 12, 8), leg('Right', 28, 44, 16, 8),
                   arm('Left', -45, 35, 35), arm('Right', -45, 35, 35),
                   spine(-18, side=-6, neck=10, head=10), {'Hips': [('R', 20), ('F', -8)]})
QC_IMPACTO_H = (-0.04, -0.10, -0.16)

QC_SOLO = merge(leg('Left', 58, 64, 8, 12), leg('Right', 46, 48, 6, 10),
                arm('Left', -52, -32, 22), arm('Right', -50, -34, 22),
                spine(-12, side=-4, neck=16, head=14), {'Hips': [('R', 54), ('F', -10)]})
QC_SOLO_H = (-0.06, -0.22, -0.76)

QC_REPOUSO = merge(leg('Left', 52, 56, 6, 12), leg('Right', 40, 40, 4, 10),
                   arm('Left', -55, -30, 26), arm('Right', -54, -30, 26),
                   spine(-8, side=-4, neck=14, head=12), {'Hips': [('R', 58), ('F', -12)]})
QC_REPOUSO_H = (-0.06, -0.22, -0.78)

# ---- caído com dor (sentado no gramado segurando a canela direita, sem atravessar o chão)
DOR_A = merge(leg('Left', 44, 42, 4, 14), leg('Right', 78, 106, 14, 6),
              arm('Left', -48, 52, 42), arm('Right', -52, 58, 48),
              spine(26, side=6, twist=10, neck=14, head=10), {'Hips': [('R', 28), ('F', -8)]})
DOR_A_H = (-0.04, -0.08, -0.78)

DOR_B = merge(leg('Left', 44, 42, 4, 14), leg('Right', 82, 110, 16, 6),
              arm('Left', -46, 55, 46), arm('Right', -50, 60, 52),
              spine(32, side=8, twist=12, neck=18, head=14), {'Hips': [('R', 26), ('F', -8)]})
DOR_B_H = (-0.04, -0.08, -0.78)

# ---- levantar atlético do gramado (impulso de mão e um joelho até ficar em pé)
LEV_APOIO = merge(leg('Left', 64, 98, 16, 10), leg('Right', 42, 86, 12, 8),
                  arm('Left', -38, 48, 45), arm('Right', -58, 22, 24),
                  spine(28, side=8, neck=8, head=4), {'Hips': [('R', -28), ('F', 24)]})
LEV_APOIO_H = (0.08, 0.06, -0.62)

LEV_JOELHO = merge(leg('Left', 76, 88, 14, 8), leg('Right', 18, 76, 24, 6),
                   arm('Left', -58, 32, 48), arm('Right', -62, 18, 42),
                   spine(24, neck=8, head=2))
LEV_JOELHO_H = (0.02, 0.02, -0.36)

# ---- passe curto com a chapa do pé (toque técnico rápido, quadril direito aberto, sem travar a corrida)
PC_ARMAR = merge(leg('Left', 26, 40, 12, 7), leg('Right', -16, 48, 6, 12, -28),
                 arm('Left', -46, 28, 46), arm('Right', -62, -12, 52),
                 spine(14, side=-5, twist=-10, neck=5, head=-4), {'Hips': [('U', -10)]})
PC_ARMAR_H = (-0.03, 0.01, -0.06)

PC_IMPACTO = merge(leg('Left', 20, 36, 10, 6), leg('Right', 34, 18, 12, 8, -32),
                   arm('Left', -48, 18, 52), arm('Right', -58, 16, 48),
                   spine(12, side=-6, twist=12, neck=4, head=-3), {'Hips': [('U', 8)]})
PC_IMPACTO_H = (-0.02, 0.03, -0.05)

PC_FINAL = merge(leg('Left', 16, 34, 8, 5), leg('Right', 44, 28, 8, 3, -20),
                 arm('Left', -56, 8, 55), arm('Right', -54, 26, 54),
                 spine(10, side=-4, twist=16, neck=4, head=-2), {'Hips': [('U', 12)]})
PC_FINAL_H = (-0.01, 0.03, -0.05)

# ---- passe longo / cruzamento (batida firme de peito/três dedos por baixo da bola com braço de equilíbrio)
PL_ARMAR = merge(leg('Left', 34, 48, 14, 11), leg('Right', -32, 78, -22, 10, -10),
                 arm('Left', -26, 36, 34), arm('Right', -58, -28, 48),
                 spine(10, side=-10, twist=-18, neck=6, head=-4), {'Hips': [('F', -8), ('U', -16)]})
PL_ARMAR_H = (-0.06, -0.02, -0.09)

PL_IMPACTO = merge(leg('Left', 22, 34, 8, 9), leg('Right', 38, 14, -18, 6, -14),
                   arm('Left', -34, 18, 44), arm('Right', -52, 24, 46),
                   spine(6, side=-14, twist=16, neck=6, head=-2), {'Hips': [('F', -10), ('U', 14)]})
PL_IMPACTO_H = (-0.05, 0.03, -0.06)

PL_FINAL = merge(leg('Left', 14, 28, 4, 8), leg('Right', 62, 34, -8, -6, -10),
                 arm('Left', -52, -10, 56), arm('Right', -46, 38, 58),
                 spine(4, side=-10, twist=24, neck=5, head=0), {'Hips': [('F', -6), ('U', 20)]})
PL_FINAL_H = (-0.04, 0.04, -0.05)

# ---- domínio orientado amortecido (chapa macia trazendo a bola para o controle)
DOM_RECEBE = merge(leg('Left', 24, 40, 12, 6), leg('Right', 36, 44, 10, 10, -24),
                   arm('Left', -54, 22, 50), arm('Right', -58, 14, 48),
                   spine(14, side=-4, twist=-6, neck=6, head=-6))
DOM_RECEBE_H = (-0.02, -0.01, -0.06)

DOM_AMORTECE = merge(leg('Left', 22, 38, 10, 5), leg('Right', 18, 38, 8, 7, -14),
                     arm('Left', -60, 18, 52), arm('Right', -62, 18, 52),
                     spine(12, side=-2, twist=0, neck=5, head=-4))
DOM_AMORTECE_H = (-0.01, -0.02, -0.06)
