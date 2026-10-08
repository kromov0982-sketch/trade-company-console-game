shader_type canvas_item;
uniform vec4 paper : source_color = vec4(0.86, 0.79, 0.62, 1.0);
float hash(vec2 p) { return fract(sin(dot(p, vec2(127.1,311.7))) * 43758.5453); }
float noise(vec2 p) {
    vec2 i=floor(p), f=fract(p); f=f*f*(3.0-2.0*f);
    return mix(mix(hash(i),hash(i+vec2(1,0)),f.x),mix(hash(i+vec2(0,1)),hash(i+vec2(1,1)),f.x),f.y);
}
void fragment() {
    vec2 p=UV;
    float grain=noise(p*vec2(900.0,600.0));
    float cloud=noise(p*7.0)*0.6+noise(p*19.0)*0.4;
    float edge=min(min(p.x,1.0-p.x),min(p.y,1.0-p.y));
    float age=(1.0-smoothstep(0.0,0.095,edge))*0.12;
    vec3 tint=paper.rgb+vec3((grain-0.5)*0.055+(cloud-0.5)*0.075)-vec3(age,age*0.9,age*0.7);
    COLOR=vec4(tint,paper.a);
}
