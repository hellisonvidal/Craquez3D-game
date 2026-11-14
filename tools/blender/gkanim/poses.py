# Poses-chave do goleiro (personagem olhando para -Y; mergulho base para a DIREITA do goleiro).
# Eixos (ver posekit): R = flexão (+ coxa/braço p/ frente quando aponta p/ baixo; - inclina tronco p/ frente),
# F = inclinação lateral (+ tronco/quadril rola p/ a direita; + ergue o braço esquerdo / abre a perna esquerda),
# U = giro (- leva o braço esquerdo p/ frente; + leva o braço direito p/ frente).
def leg(side, thigh, knee, foot, spread=10, twist=0):
    s=1 if side=='Left' else -1
    return {side+'UpLeg':[('R',thigh),('F',spread*s),('U',twist*s)], side+'Leg':[('R',-knee)], side+'Foot':[('R',foot)]}
def legs(thigh, knee, foot, spread=10):
    return merge(leg('Left',thigh,knee,foot,spread), leg('Right',thigh,knee,foot,spread))
def arm(side, up, fwd, elbow, shoulder=0, hand=0, twist=0):
    s=1 if side=='Left' else -1
    return {side+'Shoulder':[('F',shoulder*s)], side+'Arm':[('F',up*s),('U',-fwd*s),('R',twist)],
            side+'ForeArm':[('U',-elbow*s)], side+'Hand':[('R',hand)]}
def arms(up, fwd, elbow, shoulder=0, hand=0):
    return merge(arm('Left',up,fwd,elbow,shoulder,hand), arm('Right',up,fwd,elbow,shoulder,hand))
def spine(lean, side=0, twist=0, neck=0, head=0):
    return {'Spine':[('R',-lean*0.45),('F',side*0.4),('U',twist*0.4)],'Spine1':[('R',-lean*0.35),('F',side*0.35),('U',twist*0.35)],
            'Spine2':[('R',-lean*0.2),('F',side*0.25),('U',twist*0.25)],'Neck':[('R',neck)],'Head':[('R',head)]}
def merge(*ds):
    out={}
    for d in ds:
        for k,v in d.items(): out.setdefault(k,[]).extend(v)
    return out

# ---- espera (pronto para defender)
READY=merge(legs(48,76,28,12), arms(-55,45,65,-5,-10), spine(36,neck=16,head=16))
READY_H=(0,-0.10,-0.18)
READY2=merge(legs(52,82,30,12), arms(-52,48,68,-5,-10), spine(38,neck=17,head=16))   # respiração/balanço
READY2_H=(0,-0.11,-0.21)

# ---- deslocamento lateral para a direita
SHUF_A=merge(leg('Left',44,72,26,4), leg('Right',44,72,26,4), arms(-50,45,65,-5,-10), spine(34,neck=15,head=15))
SHUF_A_H=(0.0,-0.09,-0.17)
SHUF_B=merge(leg('Left',44,70,26,16), leg('Right',40,62,24,22), arms(-48,45,62,-5,-10), spine(34,side=4,neck=15,head=15))
SHUF_B_H=(0.06,-0.09,-0.19)

# ---- mergulho para a direita
D_LOAD=merge(leg('Left',30,55,20,22), leg('Right',62,95,34,8), arm('Left',-30,60,50), arm('Right',10,40,40),
             spine(30,side=14,neck=12,head=10), {'Hips':[('F',12)]})
D_LOAD_H=(0.12,-0.08,-0.28)
D_LAUNCH=merge(leg('Left',-5,10,-15,20), leg('Right',40,70,10,14), arm('Left',90,25,10,25), arm('Right',95,20,8,25),
               spine(12,side=20,neck=6,head=6), {'Hips':[('F',48)]})
D_LAUNCH_H=(0.35,0,-0.18)
D_FLY=merge(leg('Left',5,15,-25,8), leg('Right',15,25,-20,6), arm('Left',98,12,4,30), arm('Right',98,8,4,30),
            spine(4,side=6,neck=4,head=-4), {'Hips':[('F',84)]})
D_FLY_H=(0.55,0,-0.42)
D_LAND=merge(leg('Left',20,35,-10,6), leg('Right',30,45,-5,4), arm('Left',95,40,25,22), arm('Right',95,35,25,22),
             spine(14,side=0,neck=8,head=4), {'Hips':[('F',90)]})
D_LAND_H=(0.7,0,-0.76)
D_LIE=merge(leg('Left',35,55,0,4), leg('Right',45,70,5,2), arm('Left',70,75,70), arm('Right',60,75,75),
            spine(22,neck=10,head=6), {'Hips':[('F',92)]})
D_LIE_H=(0.7,0,-0.78)

# ---- levantar (deitado sobre o lado direito)
G_PUSH=merge(leg('Left',60,100,20,10), leg('Right',70,120,30,4), arm('Left',-20,70,40), arm('Right',-60,30,20),
             spine(30,neck=10,head=8), {'Hips':[('F',55)]})
G_PUSH_H=(0.45,0,-0.6)
G_KNEEL=merge(leg('Left',85,90,5,10), leg('Right',5,100,40,6), arms(-50,40,50), spine(25,neck=10,head=10))
G_KNEEL_H=(0.05,0.02,-0.45)

# ---- salto (bola alta)
J_CROUCH=merge(legs(72,112,40,10), arms(-70,-25,25), spine(42,neck=18,head=18))
J_CROUCH_H=(0,-0.15,-0.33)
J_TAKE=merge(leg('Left',75,95,10,6), leg('Right',-5,8,-25,6), arms(85,15,10,20), spine(-4,neck=-4,head=-12))
J_TAKE_H=(0,0,0)
J_PEAK=merge(leg('Left',60,85,0,6), leg('Right',15,40,-20,6), arms(95,8,5,28), spine(-8,neck=-8,head=-20))
J_PEAK_H=(0,0,-0.05)
J_CATCH=merge(leg('Left',40,60,10,8), leg('Right',30,45,0,8), arms(25,55,65), spine(12,neck=4,head=0))
J_CATCH_H=(0,0,-0.1)
J_LAND=merge(legs(55,90,32,12), arms(-10,65,110), spine(30,neck=12,head=10))
J_LAND_H=(0,-0.11,-0.24)

# ---- defesa no peito / encaixe
B_REACH=merge(legs(40,60,22,12), arms(5,75,30), spine(20,neck=8,head=6))
B_REACH_H=(0,-0.07,-0.12)
B_HUG=merge(legs(50,80,28,12), arms(-20,55,90,-5), spine(45,neck=20,head=20))
B_HUG_H=(0,-0.1,-0.2)
