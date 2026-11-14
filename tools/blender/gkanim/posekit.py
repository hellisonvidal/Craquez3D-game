# Kit de poses para o esqueleto Mixamo (T-pose canônica, personagem olhando para -Y no Blender).
# Rotações em graus nos eixos do PERSONAGEM, aplicadas no referencial do pai já posado:
#   'R' = eixo para a direita do personagem (+ = flexão p/ frente de um osso que aponta p/ baixo),
#   'F' = eixo para frente (+ = rola o osso para a direita do personagem... ver testes),
#   'U' = eixo para cima (+ = gira para a esquerda do personagem, vista de cima).
import bpy, math
from mathutils import Matrix, Vector, Quaternion
P='mixamorig:'
AX={'R':Vector((-1,0,0)),'F':Vector((0,-1,0)),'U':Vector((0,0,1))}

class Rig:
    def __init__(self, arm):
        self.arm=arm
        self.M=arm.matrix_world; self.Mi=self.M.inverted()
        bpy.context.view_layer.update()
        self.order=[b.name for b in arm.data.bones]  # pais antes dos filhos
        self.restW={n:(self.M@arm.pose.bones[n].matrix).copy() for n in self.order}
        for pb in arm.pose.bones: pb.rotation_mode='QUATERNION'
    def reset(self):
        for pb in self.arm.pose.bones: pb.matrix_basis=Matrix()
        bpy.context.view_layer.update()
    def pose(self, spec, hips_offset=(0,0,0)):
        """spec: {'LeftUpLeg':[('R',30),('F',-5)], ...}; hips_offset em metros no referencial do personagem (direita, frente, cima)."""
        self.reset()
        delta={}
        for n in self.order:
            short=n.replace(P,'')
            pb=self.arm.pose.bones[n]
            par=pb.parent.name if pb.parent else None
            pq=delta.get(par, Quaternion())
            acc={'U':0.0,'R':0.0,'F':0.0}
            for ax,deg in spec.get(short,[]): acc[ax]+=deg
            R=Quaternion()
            for ax in 'URF':  # ordem fixa: gira (U), flexiona (R), inclina (F)
                if acc[ax]: R=Quaternion(AX[ax], math.radians(acc[ax]))@R
            Rw=pq@R@pq.inverted()
            bpy.context.view_layer.update()
            W=self.M@pb.matrix
            h=W.to_translation()
            Wn=Matrix.Translation(h)@Rw.to_matrix().to_4x4()@Matrix.Translation(-h)@W
            if short=='Hips':
                o=AX['R']*hips_offset[0]+AX['F']*hips_offset[1]+AX['U']*hips_offset[2]
                Wn=Matrix.Translation(o)@Wn
            pb.matrix=self.Mi@Wn
            delta[n]=(Wn.to_quaternion()@self.restW[n].to_quaternion().inverted()).normalized()
        bpy.context.view_layer.update()
    def key(self, frame, prev=None):
        out={}
        for pb in self.arm.pose.bones:
            q=pb.rotation_quaternion.copy()
            if prev is not None and pb.name in prev and prev[pb.name].dot(q)<0: q.negate()
            pb.rotation_quaternion=q
            pb.keyframe_insert('rotation_quaternion', frame=frame)
            if pb.parent is None: pb.keyframe_insert('location', frame=frame)
            out[pb.name]=q
        return out

def mirror(spec, hips_offset=(0,0,0)):
    """Espelha uma pose (esquerda<->direita): inverte rotações nos eixos F e U."""
    out={}
    for k,v in spec.items():
        k2=k.replace('Left','#').replace('Right','Left').replace('#','Right')
        out[k2]=[(ax, d if ax=='R' else -d) for ax,d in v]
    return out, (-hips_offset[0],hips_offset[1],hips_offset[2])

def blend(a, b, t):
    keys=set(a)|set(b); out={}
    for k in keys:
        da={ax:0.0 for ax in 'RFU'}; db={ax:0.0 for ax in 'RFU'}
        for ax,d in a.get(k,[]): da[ax]+=d
        for ax,d in b.get(k,[]): db[ax]+=d
        out[k]=[(ax, da[ax]*(1-t)+db[ax]*t) for ax in 'URF']
    return out
