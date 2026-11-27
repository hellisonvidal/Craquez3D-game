# Poses-chave nativas completas do goleiro sobre o esqueleto corrigido de Models/Goleiro.fbx (olhando para -Y).
# Eixos (tools/blender/gkanim/posekit.py):
#   R = flexão (+ coxa/braço p/ frente quando aponta p/ baixo; - inclina tronco p/ frente),
#   F = inclinação lateral (+ tronco/quadril rola p/ a direita; + ergue o braço esquerdo / abre a perna esquerda),
#   U = giro (- leva o braço esquerdo p/ frente; + leva o braço direito p/ frente).
def leg(side, thigh, knee, foot, spread=8, twist=0):
    s = 1 if side == 'Left' else -1
    return {
        side + 'UpLeg': [('R', thigh), ('F', spread * s), ('U', twist * s)],
        side + 'Leg': [('R', -knee)],
        side + 'Foot': [('R', foot)],
    }


def legs(thigh, knee, foot, spread=8):
    return merge(leg('Left', thigh, knee, foot, spread), leg('Right', thigh, knee, foot, spread))


def arm(side, up, fwd, elbow, shoulder=0, hand=0, twist=0):
    s = 1 if side == 'Left' else -1
    return {
        side + 'Shoulder': [('F', shoulder * s)],
        side + 'Arm': [('F', up * s), ('U', -fwd * s), ('R', twist)],
        side + 'ForeArm': [('U', -elbow * s)],
        side + 'Hand': [('R', hand)],
    }


def arms(up, fwd, elbow, shoulder=0, hand=0):
    return merge(arm('Left', up, fwd, elbow, shoulder, hand), arm('Right', up, fwd, elbow, shoulder, hand))


def spine(lean, side=0, twist=0, neck=0, head=0):
    return {
        'Spine': [('R', -lean * 0.45), ('F', side * 0.4), ('U', twist * 0.4)],
        'Spine1': [('R', -lean * 0.35), ('F', side * 0.35), ('U', twist * 0.35)],
        'Spine2': [('R', -lean * 0.2), ('F', side * 0.25), ('U', twist * 0.25)],
        'Neck': [('R', neck)],
        'Head': [('R', head)],
    }


def merge(*ds):
    out = {}
    for d in ds:
        for k, v in d.items():
            out.setdefault(k, []).extend(v)
    return out


# ---- 1. Postura parada plantada de goleiro (GK_Parado: pés 100% imóveis, base atlética alerta, luvas prontas)
IDLE_A = merge(legs(22, 38, 16, 8.5), arms(-56, 36, 58, -3, -6), spine(18, neck=8, head=8))
IDLE_A_H = (0.0, -0.04, -0.065)

IDLE_B = merge(legs(22, 38, 16, 8.5), arms(-54.5, 37.5, 60, -2, -6), spine(19.5, neck=8.8, head=8.5))
IDLE_B_H = (0.0, -0.04, -0.065)

# ---- 2. Postura pronta mais baixa em chute iminente (GK_Pronto)
READY = merge(legs(34, 56, 22, 10), arms(-52, 42, 62, -4, -8), spine(26, neck=12, head=12))
READY_H = (0.0, -0.06, -0.11)
READY2 = merge(legs(36, 59, 23, 10), arms(-50, 44, 64, -4, -8), spine(27.5, neck=13, head=12))
READY2_H = (0.0, -0.065, -0.12)

# ---- 3. Deslocamento lateral (passada caranguejo no arco, sem cruzar pernas nem levantar braços em T)
SHUF_A = merge(leg('Left', 30, 50, 20, 5), leg('Right', 30, 50, 20, 5), arms(-52, 40, 62, -4, -8), spine(24, neck=11, head=11))
SHUF_A_H = (-0.04, -0.05, -0.10)
SHUF_B = merge(leg('Left', 28, 46, 18, 16), leg('Right', 34, 56, 22, 18), arms(-48, 42, 58, -4, -8), spine(25, side=4, neck=11, head=11))
SHUF_B_H = (0.06, -0.05, -0.12)

# ---- 4. Caminhada alerta para frente (GK_Andar)
W_L = merge(leg('Left', 32, 24, 8, 5), leg('Right', -10, 28, -14, 5),
            arm('Left', -64, -8, 42), arm('Right', -58, 28, 52), spine(14, twist=6, neck=6, head=4))
W_L_H = (0.0, 0.0, -0.04)
W_M1 = merge(leg('Left', 12, 18, 4, 5), leg('Right', 18, 46, 8, 5),
             arm('Left', -62, 10, 46), arm('Right', -62, 10, 46), spine(14, neck=6, head=4))
W_M1_H = (0.0, 0.0, -0.025)
W_R = merge(leg('Left', -10, 28, -14, 5), leg('Right', 32, 24, 8, 5),
            arm('Left', -58, 28, 52), arm('Right', -64, -8, 42), spine(14, twist=-6, neck=6, head=4))
W_R_H = (0.0, 0.0, -0.04)
W_M2 = merge(leg('Left', 18, 46, 8, 5), leg('Right', 12, 18, 4, 5),
             arm('Left', -62, 10, 46), arm('Right', -62, 10, 46), spine(14, neck=6, head=4))
W_M2_H = (0.0, 0.0, -0.025)

# ---- 5. Saída rápida do gol / corrida de fechamento de ângulo (GK_Correr)
R_L = merge(leg('Left', 54, 42, 12, 5), leg('Right', -24, 34, -22, 5),
            arm('Left', -54, -26, 65), arm('Right', -46, 52, 74), spine(24, twist=10, neck=10, head=6))
R_L_H = (0.0, 0.02, -0.06)
R_M1 = merge(leg('Left', 18, 30, 6, 5), leg('Right', 34, 78, 12, 5),
             arm('Left', -52, 14, 66), arm('Right', -52, 14, 66), spine(24, neck=10, head=6))
R_M1_H = (0.0, 0.02, -0.03)
R_R = merge(leg('Left', -24, 34, -22, 5), leg('Right', 54, 42, 12, 5),
            arm('Left', -46, 52, 74), arm('Right', -54, -26, 65), spine(24, twist=-10, neck=10, head=6))
R_R_H = (0.0, 0.02, -0.06)
R_M2 = merge(leg('Left', 34, 78, 12, 5), leg('Right', 18, 30, 6, 5),
             arm('Left', -52, 14, 66), arm('Right', -52, 14, 66), spine(24, neck=10, head=6))
R_M2_H = (0.0, 0.02, -0.03)

# ---- 6. Recuo olhando para a bola (GK_Recuar: passos curtos para trás com luvas prontas)
BK_A = merge(leg('Left', 14, 44, 16, 7), leg('Right', 34, 54, 18, 7),
             arm('Left', -52, 34, 58), arm('Right', -56, 22, 54), spine(20, twist=-5, neck=10, head=8))
BK_A_H = (0.0, -0.04, -0.08)
BK_B = merge(leg('Left', 34, 54, 18, 7), leg('Right', 14, 44, 16, 7),
             arm('Left', -56, 22, 54), arm('Right', -52, 34, 58), spine(20, twist=5, neck=10, head=8))
BK_B_H = (0.0, -0.04, -0.08)

# ---- 7. Mergulho elástico para a DIREITA (braços paralelos sem cruzar, aterrissagem amortecida acima da grama)
D_LOAD = merge(leg('Left', 28, 52, 18, 18), leg('Right', 56, 86, 30, 8),
               arm('Left', -28, 48, 44), arm('Right', 12, 36, 34),
               spine(26, side=14, neck=10, head=8), {'Hips': [('F', 14)]})
D_LOAD_H = (0.10, -0.05, -0.20)

D_LAUNCH = merge(leg('Left', -4, 14, -16, 16), leg('Right', 34, 60, 8, 12),
                 arm('Left', 74, 24, 14, 18), arm('Right', 86, 18, 10, 22),
                 spine(10, side=22, neck=6, head=4), {'Hips': [('F', 48)]})
D_LAUNCH_H = (0.28, 0.0, -0.14)

D_FLY = merge(leg('Left', 8, 18, -20, 8), leg('Right', 18, 28, -14, 6),
              arm('Left', 82, 16, 8, 22), arm('Right', 92, 12, 6, 24),
              spine(4, side=8, neck=4, head=-4), {'Hips': [('F', 80)]})
D_FLY_H = (0.46, 0.0, -0.36)

D_LAND = merge(leg('Left', 22, 38, -8, 6), leg('Right', 32, 48, -4, 4),
               arm('Left', 68, 38, 28, 16), arm('Right', 76, 32, 26, 18),
               spine(12, side=4, neck=8, head=4), {'Hips': [('F', 86)]})
D_LAND_H = (0.56, 0.0, -0.68)

D_LIE = merge(leg('Left', 34, 54, 0, 5), leg('Right', 44, 66, 4, 3),
              arm('Left', 42, 62, 58), arm('Right', 48, 58, 62),
              spine(18, neck=10, head=6), {'Hips': [('F', 88)]})
D_LIE_H = (0.56, 0.0, -0.72)

# ---- 8. Levantar atlético após mergulho (GK_Levantar)
G_PUSH = merge(leg('Left', 58, 96, 18, 10), leg('Right', 64, 108, 26, 5),
               arm('Left', -22, 62, 42), arm('Right', -56, 28, 22),
               spine(28, neck=10, head=8), {'Hips': [('F', 48)]})
G_PUSH_H = (0.34, 0.0, -0.54)

G_KNEEL = merge(leg('Left', 78, 86, 8, 9), leg('Right', 12, 92, 34, 6),
                arms(-48, 38, 50), spine(22, neck=10, head=10))
G_KNEEL_H = (0.04, 0.02, -0.38)

# ---- 9. Salto alto para encaixar cruzamento/ângulo (GK_Salto: joelho de proteção erguido, luvas no alto)
J_CROUCH = merge(legs(58, 94, 34, 10), arms(-62, -18, 28), spine(34, neck=14, head=14))
J_CROUCH_H = (0.0, -0.10, -0.24)

J_TAKE = merge(leg('Left', 72, 92, 10, 6), leg('Right', -4, 12, -22, 6),
               arms(78, 20, 14, 18), spine(-4, neck=-4, head=-12))
J_TAKE_H = (0.0, 0.02, 0.14)

J_PEAK = merge(leg('Left', 64, 86, 4, 6), leg('Right', 12, 32, -18, 6),
               arms(88, 14, 10, 24), spine(-6, neck=-6, head=-16))
J_PEAK_H = (0.0, 0.03, 0.28)

J_CATCH = merge(leg('Left', 40, 60, 10, 8), leg('Right', 26, 42, 0, 8),
                arms(24, 54, 66), spine(12, neck=4, head=2))
J_CATCH_H = (0.0, 0.01, 0.08)

J_LAND = merge(legs(44, 72, 26, 10), arms(-16, 58, 96), spine(26, neck=10, head=10))
J_LAND_H = (0.0, -0.07, -0.16)

# ---- 10. Saída de soco no alto em cruzamentos (GK_Soco: impulsão vertical + soco firme acima da cabeça!)
P_TAKE = merge(leg('Left', 68, 88, 8, 6), leg('Right', -6, 14, -22, 6),
               arm('Left', 35, 45, 52, 10), arm('Right', 72, 28, 28, 18),
               spine(-2, twist=-14, neck=-6, head=-14))
P_TAKE_H = (0.0, 0.03, 0.16)

P_PUNCH = merge(leg('Left', 58, 82, 2, 6), leg('Right', 10, 30, -18, 6),
                arm('Left', 48, 38, 42, 12), arm('Right', 96, 16, 6, 26),
                spine(-6, twist=18, neck=-8, head=-16))
P_PUNCH_H = (0.0, 0.05, 0.30)

# ---- 11. Defesa firme / encaixe no peito (GK_Defesa)
B_REACH = merge(legs(34, 54, 20, 10), arms(4, 68, 32), spine(18, neck=8, head=6))
B_REACH_H = (0.0, -0.05, -0.09)

B_HUG = merge(legs(42, 68, 24, 10), arms(-22, 54, 88, -4), spine(36, neck=16, head=16))
B_HUG_H = (0.0, -0.08, -0.15)

# ---- 12. Abafamento / defesa baixa com os pés e corpo em 1x1 (GK_Carrinho: K-block rente à grama!)
K_DROP = merge(leg('Left', 46, 76, 16, 18), leg('Right', 18, 98, -12, 26, -18),
               arm('Left', -24, 34, 24), arm('Right', -20, 38, 22),
               spine(20, side=10, neck=8, head=-4), {'Hips': [('F', 12)]})
K_DROP_H = (0.14, 0.04, -0.34)

K_SPREAD = merge(leg('Left', 58, 62, 12, 28), leg('Right', 12, 108, -16, 32, -24),
                 arm('Left', -18, 28, 18), arm('Right', -14, 32, 18),
                 spine(16, side=14, neck=8, head=-6), {'Hips': [('F', 18)]})
K_SPREAD_H = (0.24, 0.06, -0.52)

# ---- 13. Reposição com a mão (GK_Lancar: lançamento de goleiro por cima/ombro direito, braço esquerdo apontando)
TH_WIND = merge(leg('Left', 28, 42, 12, 8), leg('Right', -8, 32, -6, 8),
                arm('Left', -22, 58, 24, 4), arm('Right', 32, -38, 46, 12),
                spine(10, twist=-26, neck=4, head=-6), {'Hips': [('U', -18)]})
TH_WIND_H = (-0.03, 0.02, -0.07)

TH_REL = merge(leg('Left', 36, 46, 14, 7), leg('Right', -16, 26, -16, 7),
               arm('Left', -48, 22, 52), arm('Right', 56, 52, 14, 18),
               spine(22, twist=24, neck=8, head=-4), {'Hips': [('U', 18)]})
TH_REL_H = (0.02, 0.08, -0.08)

TH_FOL = merge(leg('Left', 30, 42, 12, 7), leg('Right', 8, 30, 0, 7),
               arm('Left', -58, 10, 56), arm('Right', -28, 64, 34, 6),
               spine(26, twist=30, neck=10, head=-2), {'Hips': [('U', 20)]})
TH_FOL_H = (0.02, 0.10, -0.09)

# ---- 14. Chutão / Tiro de Meta (GK_Chutao: batida potente e natural na altura do quadril, sem chute de karatê)
PK_DROP = merge(leg('Left', 32, 46, 14, 10), leg('Right', -34, 82, -24, 8),
                arm('Left', -24, 42, 32), arm('Right', -54, -26, 46),
                spine(12, side=-10, twist=-18, neck=8, head=6), {'Hips': [('F', -8), ('U', -16)]})
PK_DROP_H = (-0.06, 0.02, -0.08)

PK_HIT = merge(leg('Left', 20, 32, 8, 9), leg('Right', 42, 16, -20, 6, -10),
               arm('Left', -32, 22, 44), arm('Right', -48, 26, 48),
               spine(6, side=-14, twist=18, neck=6, head=2), {'Hips': [('F', -10), ('U', 14)]})
PK_HIT_H = (-0.05, 0.05, -0.05)

PK_FOL = merge(leg('Left', 12, 26, 4, 8), leg('Right', 68, 32, -8, -4, -8),
               arm('Left', -52, -12, 56), arm('Right', -42, 42, 58),
               spine(2, side=-10, twist=26, neck=4, head=0), {'Hips': [('F', -6), ('U', 22)]})
PK_FOL_H = (-0.04, 0.06, -0.04)
