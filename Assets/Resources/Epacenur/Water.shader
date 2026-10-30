// Ponds and rivers: ripples (_MainTex) that drift along each river the way it runs, and lie almost still on
// ponds. The flow (tiles a second, from the overworld) is in each vertex's second uv; ripples are laid out by
// the vertex's own tile position (the map's local space, which doesn't move when the origin does). Two copies
// of the ripples, half a cycle apart, take turns so the drift never stretches them.
Shader "Epacenur/Water"
{
    Properties
    {
        _Color ("Color", Color) = (0.2,0.42,0.7,1)
        _MainTex ("Ripples", 2D) = "white" {}
        _Scale ("Tiles per ripple texture", Float) = 4
        _Cycle ("Seconds per cycle", Float) = 1.5
        _Glossiness ("Smoothness", Range(0,1)) = 0.75
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 200

        CGPROGRAM
        #pragma surface surf Standard vertex:vert fullforwardshadows
        #pragma target 3.0

        sampler2D _MainTex;
        fixed4 _Color;
        float _Scale;
        float _Cycle;
        half _Glossiness;

        struct Input
        {
            float2 flow;
            float2 spot;
        };

        void vert(inout appdata_full v, out Input o)
        {
            UNITY_INITIALIZE_OUTPUT(Input, o);
            o.flow = v.texcoord1.xy;
            o.spot = v.vertex.xz;
        }

        void surf(Input IN, inout SurfaceOutputStandard o)
        {
            float t = _Time.y / _Cycle;
            float p0 = frac(t), p1 = frac(t + 0.5);
            float turn = abs(1 - 2 * p0);
            float2 uv = IN.spot / _Scale;
            float2 run = IN.flow * _Cycle / _Scale;
            half a = tex2D(_MainTex, uv - run * p0).r;
            half b = tex2D(_MainTex, uv - run * p1 + float2(0.37, 0.21)).r;
            half moving = lerp(a, b, turn);
            half still = tex2D(_MainTex, uv * 0.6 + float2(_Time.y * 0.012, _Time.y * 0.008)).r;
            half r = lerp(still, moving, saturate(length(IN.flow) * 1.5));
            o.Albedo = _Color.rgb * (0.78 + 0.45 * r);
            o.Metallic = 0;
            o.Smoothness = _Glossiness;
            o.Alpha = 1;
        }
        ENDCG
    }
    FallBack "Diffuse"
}
