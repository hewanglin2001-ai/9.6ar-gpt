"""Headless fragment-program smoke/visual test; does NOT replace Unity/Metal QA.

pip install numpy pillow moderngl mediapipe scikit-image
Usage: python verify_skin.py --work /path/to/qa --egl /path/to/libEGL.so.1
work must contain astronaut.png and astronaut-landmarks.json (MediaPipe 478 pts).
The shader function bodies are loaded from the project, translated to GLSL and
executed by Mesa. Unity vertex/UI plumbing is intentionally not simulated.
"""
import argparse, json, re, textwrap
from pathlib import Path
import numpy as np
from PIL import Image
import moderngl

ROOT = Path(__file__).resolve().parents[2]
SRC = ROOT / "Assets/MediaPipeUnity/Samples/Scenes/Face Landmark Detection"
VERT = """#version 330
in vec2 position; out vec2 uv;
void main(){ uv=position*0.5+0.5; gl_Position=vec4(position,0,1); }
"""

def translate(s):
    s = re.sub(r'#include[^\n]*', '', s)
    s = re.sub(r'\[(unroll|loop)\]', '', s)
    s = re.sub(r':\s*SV_Target', '', s)
    for a,b in [('float2','vec2'),('float3','vec3'),('float4','vec4'),
                ('tex2D','texture'),('lerp','mix'),('frac','fract')]:
        s = re.sub(r'\b'+a+r'\b',b,s)
    s = s.replace('(int)(i.uv.x * 6)', 'int(i.uv.x * 6)')
    s = re.sub(r'(vec[234]\s+\w+\s*=)\s*0;',r'\1 vec3(0);',s)
    s = s.replace('return 0;', 'return vec4(0);')
    s = s.replace('any(uv < 0.0)', 'any(lessThan(uv,vec2(0)))')
    s = s.replace('any(uv > 1.0)', 'any(greaterThan(uv,vec2(1)))')
    s = s.replace('all(atlas >= 0)', 'all(greaterThanEqual(atlas,vec2(0)))')
    s = s.replace('all(atlas <= 1)', 'all(lessThanEqual(atlas,vec2(1)))')
    return s

def uniforms(s):
    return re.sub(r'(?m)^(\s*)(float[234]?|sampler2D)(\s+_[^;]+;)',r'\1uniform \2\3',s)

def shader_body(composite=False):
    common=(SRC/'FacelessSkinCommon.cginc').read_text()
    s=(SRC/('FacelessSkinComposite.shader' if composite else 'FacelessSkinReconstruction.shader')).read_text()
    if composite:
        globals=s[s.index('sampler2D _MainTex'):s.index('struct appdata')]
        body=s[s.index('float4 frag(v2f'):s.index('ENDCG')]
        structs='struct v2f {vec2 uv; vec4 color; vec4 local;};'
    else:
        s=s[s.index('sampler2D _MainTex'):s.index('ENDCG')]
        globals=s[:s.index('float4 Gaussian')]
        body=s[s.index('float4 Gaussian'):]
        structs='struct v2f_img {vec2 uv;};'
    return translate(uniforms(common)+uniforms(globals)+structs+body)

def fit_regions(points):
    source=(SRC/'FacelessRegions.cs').read_text()
    groups=re.findall(r'new\[\] \{([\d,\s]+)\}',source)
    ids=[list(map(int,re.findall(r'\d+',g))) for g in groups]
    boundary=list(map(int,re.findall(r'\d+',source.split('BoundaryIndices =')[1].split('};')[0])))
    p=points
    width=np.linalg.norm(p[234]-p[454]);height=np.linalg.norm(p[10]-p[152])
    up=(p[10]-p[152])/height; right=np.array([up[1],-up[0]])
    if right@(p[454]-p[234])<0:right=-right
    origin=(p[10]+p[152])*0.5
    u=right*width*1.25;v=up*height*1.16
    axes=[p[133]-p[33],p[263]-p[362],right,p[327]-p[98],p[291]-p[61],p[291]-p[61],right]
    centers=[]; aa=[]
    for i,(group,x) in enumerate(zip(ids,axes)):
        x=x/np.linalg.norm(x); y=np.array([-x[1],x[0]])
        q=np.stack((p[group]@x,p[group]@y),axis=-1)
        low=q.min(0);high=q.max(0);c=(low+high)/2;r=(high-low)/2+width*.022
        r[0]=max(r[0],width*(.060 if i==2 else .095 if i==6 else .018))
        r[1]=max(r[1],height*(.052 if i==4 else .015))
        enclosure=max(1,float(((np.abs(q-c)/r)**4).sum(-1).max()**.25)*1.08)
        r*=enclosure
        if i==5:r[0]*=1.10
        center=x*c[0]+y*c[1]
        centers.append([*center,*r])
        edge=min(width*.09,max(width*.013,r[0]*.75))
        aa.append([*x,edge,0])
    donors=np.array([[*p[i],width*.016,0] for i in [50,117,187,280,346,411]])
    b=np.c_[p[boundary],np.zeros((36,2))]
    values={'_Regions':np.array(centers),'_RegionAxes':np.array(aa),'_Boundary':b,
            '_Donors':donors,'_FrameOrigin':[*origin,width,height],'_FrameU':[*u,0,0],
            '_FrameV':[*v,max((p[105]-origin)@up,(p[334]-origin)@up)+height*.035,0],
            '_ContourInset':width*.018,
            '_FaceBounds':[*p[:468].min(0),*p[:468].max(0)]}
    return values,ids

def main():
    ap=argparse.ArgumentParser();ap.add_argument('--work',type=Path,required=True);ap.add_argument('--egl');a=ap.parse_args()
    kwargs={'backend':'egl'}
    if a.egl:kwargs['libegl']=a.egl
    ctx=moderngl.create_standalone_context(**kwargs)
    print('renderer:',ctx.info['GL_RENDERER'])
    image=np.asarray(Image.open(a.work/'astronaut.png').convert('RGB'),dtype=np.float32)[::-1]/255
    h,w=image.shape[:2]
    points=np.array(json.loads((a.work/'astronaut-landmarks.json').read_text()))[:,:2]
    points[:,0]*=w;points[:,1]=(1-points[:,1])*h
    vals,groups=fit_regions(points)
    vals.update(_CameraSize=[w,h,1/w,1/h],_Amount=1.,_Volume=.045,_Grain=0.,_ShowMask=0.,
                _Color=[1,1,1,1],_Stages=np.ones(7))
    buffer=ctx.buffer(np.array([-1,-1,1,-1,-1,1,1,1],dtype='f4').tobytes())
    programs={}
    for name in ['fragDonors','fragSeeds','fragGaussian','fragNormalize','fragPull','fragRelax','fragTemporal','frag']:
        composite=name=='frag'
        body=shader_body(composite)
        setup='v2f i; i.uv=uv; i.color=vec4(1); i.local=vec4(0);' if composite else 'v2f_img i; i.uv=uv;'
        frag='#version 330\n#define saturate(x) clamp(x,0.0,1.0)\nin vec2 uv; out vec4 result;\n'+body+'\nvoid main(){'+setup+'result='+name+'(i);}'
        (a.work/(name+'.glsl')).write_text(frag)
        prog=ctx.program(vertex_shader=VERT,fragment_shader=frag)
        programs[name]=(prog,ctx.simple_vertex_array(prog,buffer,'position'))
    print('compiled fragment programs:',len(programs))
    def tex(size,data=None):
        t=ctx.texture(size,4,None if data is None else data.astype('f4').tobytes(),dtype='f4')
        t.filter=(moderngl.LINEAR,moderngl.LINEAR);t.repeat_x=t.repeat_y=False
        return t
    original=tex((w,h),np.dstack((image,np.ones((h,w)))))
    def render(name,src,target,bindings=None,values=None):
        prog,vao=programs[name]
        for key,value in dict(vals,**(values or {})).items():
            if key not in prog:continue
            arr=np.array(value,dtype='f4')
            if arr.ndim>1 or key=='_Stages':prog[key].write(arr.tobytes())
            else:prog[key].value=float(arr) if arr.ndim==0 else tuple(arr)
        for unit,(key,t) in enumerate(dict(_MainTex=src,**(bindings or {})).items()):
            if key in prog:t.use(unit);prog[key].value=unit
        if '_MainTex_TexelSize' in prog:prog['_MainTex_TexelSize'].value=(1/src.width,1/src.height,src.width,src.height)
        fb=ctx.framebuffer(color_attachments=[target]);fb.use();ctx.viewport=(0,0,target.width,target.height)
        vao.render(moderngl.TRIANGLE_STRIP);fb.release()
    donors=tex((6,1));render('fragDonors',original,donors)
    sizes=[256,128,64,32,16,8,4]
    known=[tex((s,s)) for s in sizes];temp=[tex((s,s)) for s in sizes];filled=[tex((s,s)) for s in sizes]
    render('fragSeeds',original,known[0],{'_DonorTex':donors})
    for i in range(len(sizes)-1):
        render('fragGaussian',known[i],temp[i],values={'_Direction':[1,0]})
        render('fragGaussian',temp[i],known[i+1],values={'_Direction':[0,1]})
    render('fragNormalize',known[-1],filled[-1],{'_DonorTex':donors})
    for i in range(len(sizes)-2,-1,-1):
        render('fragPull',filled[i+1],filled[i],{'_KnownTex':known[i]})
        for _ in range(2):
            render('fragRelax',filled[i],temp[i],{'_KnownTex':known[i]}, {'_Direction':[1,0]})
            render('fragRelax',temp[i],filled[i],{'_KnownTex':known[i]}, {'_Direction':[0,1]})
    output=tex((w,h));render('frag',original,output,{'_SkinTex':filled[0]})
    def read(t):return np.frombuffer(t.read(),dtype='f4').reshape(t.height,t.width,4).copy()
    result=read(output);assert np.isfinite(result).all()
    seed_data=read(known[0])
    origin=np.array(vals['_FrameOrigin'][:2])
    u=np.array(vals['_FrameU'][:2]);v=np.array(vals['_FrameV'][:2])
    excluded=[]
    for landmark in [159,386,1,13,14,105,334]:
        delta=points[landmark]-origin
        atlas=np.array([delta@u/(u@u),delta@v/(v@v)])+.5
        x,y=np.clip((atlas*256).astype(int),0,255)
        confidence=float(seed_data[y,x,3])
        assert confidence<1e-6,(landmark,confidence)
        excluded.append(landmark)
    Image.fromarray((np.clip(result[::-1,:,:3],0,1)*255).astype('uint8')).save(a.work/'result.png')
    render('frag',original,output,{'_SkinTex':filled[0]}, {'_ShowMask':1.})
    debug=read(output)
    Image.fromarray((np.clip(debug[::-1,:,:3],0,1)*255).astype('uint8')).save(a.work/'regions.png')
    render('frag',original,output,{'_SkinTex':filled[0]}, {'_Amount':0.})
    passthrough=read(output)[:,:,:3]
    err=float(np.max(np.abs(passthrough-image)));assert err<1e-5,err
    # Regions are confined to the local face bounds; outer video is untouched.
    yy,xx=np.mgrid[:h,:w];b=vals['_FaceBounds']
    outside=(xx<b[0]-2)|(xx>b[2]+2)|(yy<b[1]-2)|(yy>b[3]+2)
    outside_error=float(np.max(np.abs(result[:,:,:3][outside]-image[outside])))
    assert outside_error<1e-5,outside_error
    stats={'fragment_programs':len(programs),'passthrough_max_error':err,'outside_max_error':outside_error,
           'zero_source_confidence_landmarks':excluded,
           'finite_output':True,'unity_editor_tested':False,'metal_tested':False}
    (a.work/'checks.json').write_text(json.dumps(stats,indent=2));print(stats)

if __name__=='__main__':main()
