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
