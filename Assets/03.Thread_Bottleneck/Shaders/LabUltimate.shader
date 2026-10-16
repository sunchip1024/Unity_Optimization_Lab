Shader "OptimizationLab/Ultimate"
{
 Properties { _MainTex("Scene",2D)="white"{} _Iterations("GPU samples",Int)=384 }
 SubShader { Cull Off ZWrite Off ZTest Always
 Pass {
 CGPROGRAM
 #pragma vertex vert_img
 #pragma fragment frag
 #pragma target 3.0
 #include "UnityCG.cginc"
 sampler2D _MainTex;
 float4 _MainTex_TexelSize;
 int _Iterations;
 fixed4 frag(v2f_img i):SV_Target {
   float2 uv=i.uv; float3 sum=0;
   // Real dependent texture samples: this is GPU work, not a CPU Sleep.
   [loop] for(int n=0;n<_Iterations;n++) {
      float angle=n*2.399963+_Time.y*0.1;
      uv=i.uv+float2(cos(angle),sin(angle))*_MainTex_TexelSize.xy*(1+(uint)n%9u);
      uv += sum.rg * 0.000001;
      sum+=tex2D(_MainTex,uv).rgb;
   }
   float3 baseColor=tex2D(_MainTex,i.uv).rgb;
   float edge=smoothstep(0.25,0.8,length(i.uv-0.5));
   return fixed4(lerp(baseColor,sum/max(1,_Iterations),0.32)*lerp(float3(1,1,1),float3(0.7,0.55,0.9),edge),1);
 }
 ENDCG
 }
 }
}
