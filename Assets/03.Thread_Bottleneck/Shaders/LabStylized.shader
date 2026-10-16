Shader "OptimizationLab/Stylized"
{
 Properties { _Color("Color",Color)=(1,1,1,1) _EmissionColor("Glow",Color)=(0,0,0,1) }
 SubShader {
 Tags { "RenderType"="Opaque" "DisableBatching"="True" }
 Pass {
 CGPROGRAM
 #pragma vertex vert
 #pragma fragment frag
 #pragma multi_compile_instancing
 #pragma multi_compile_fog
 #pragma instancing_options assumeuniformscaling
 #include "UnityCG.cginc"
 #include "Lighting.cginc"
 fixed4 _Color, _EmissionColor;
 struct appdata { float4 vertex:POSITION; float3 normal:NORMAL; UNITY_VERTEX_INPUT_INSTANCE_ID };
 struct v2f { float4 pos:SV_POSITION; float3 normal:TEXCOORD0; float3 world:TEXCOORD1; UNITY_FOG_COORDS(2) };
 v2f vert(appdata v) {
   UNITY_SETUP_INSTANCE_ID(v); v2f o;
   o.pos=UnityObjectToClipPos(v.vertex);
   o.normal=UnityObjectToWorldNormal(v.normal);
   o.world=mul(unity_ObjectToWorld,v.vertex).xyz;
   UNITY_TRANSFER_FOG(o,o.pos); return o;
 }
 fixed4 frag(v2f i):SV_Target {
   float diffuse=saturate(dot(normalize(i.normal),normalize(float3(-0.3,0.8,-0.45))));
   float3 col=_Color.rgb * (float3(0.25,0.27,0.38)+diffuse*float3(0.7,0.73,0.85))+_EmissionColor.rgb;
   fixed4 c=fixed4(col,1); UNITY_APPLY_FOG(i.fogCoord,c); return c;
 }
 ENDCG
 }
 }
}
