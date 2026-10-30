// Plants that sway in the wind (grass, flowers, bushes, trees' crowns). Lit like the game's other materials
// (Standard), with the wind (_EpWind: xy the way it blows times its strength, set every frame by the weather)
// pushing each vertex downwind and fluttering it. _Rooted 1: the mesh's uv.y says how free each vertex is
// (0 at a grass blade's foot, 1 at its tip); 0: the whole mesh moves alike (a tree's crown). _Sway: how far.
Shader "Epacenur/Sway"
{
    Properties
    {
        _Color ("Color", Color) = (1,1,1,1)
        _MainTex ("Texture", 2D) = "white" {}
        _Glossiness ("Smoothness", Range(0,1)) = 0.08
        _Sway ("Sway", Float) = 0.1
        _Rooted ("Rooted", Float) = 0
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 200

        CGPROGRAM
        #pragma surface surf Standard vertex:vert addshadow fullforwardshadows
        #pragma multi_compile_instancing
        #pragma target 3.0

        sampler2D _MainTex;
        fixed4 _Color;
        half _Glossiness;
        float _Sway;
        float _Rooted;
        float4 _EpWind;
        float _EpTime;

        struct Input
        {
            float2 uv_MainTex;
        };

        void vert(inout appdata_full v)
        {
            float4 wp = mul(unity_ObjectToWorld, v.vertex);
            float free = lerp(1.0, saturate(v.texcoord.y), _Rooted);
            float wave = sin(_EpTime * 1.9 + wp.x * 0.37 + wp.z * 0.29) * 0.6 + sin(_EpTime * 3.3 + wp.x * 0.83 - wp.z * 0.51) * 0.4;
            float push = 0.55 + 0.45 * wave;
            float2 lean = _EpWind.xy * push * _Sway * free;
            wp.xz += lean;
            wp.y -= length(lean) * free * 0.3;  // bending over, the tip comes down a little
            v.vertex = mul(unity_WorldToObject, wp);
        }

        void surf(Input IN, inout SurfaceOutputStandard o)
        {
            fixed4 c = tex2D(_MainTex, IN.uv_MainTex) * _Color;
            o.Albedo = c.rgb;
            o.Metallic = 0;
            o.Smoothness = _Glossiness;
            o.Alpha = 1;
        }
        ENDCG
    }
    FallBack "Diffuse"
}
