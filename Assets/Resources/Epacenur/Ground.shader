// The overworld's ground: grass, earth, rock and snow blended smoothly between the corners of its tiles. Each
// corner carries the grass's colour (vertex colour) and how much earth and rock (uv2) and snow (uv3) cover it;
// each texture's own brightness pushes it through at the edges, so the edges follow its detail (GroundBlend
// works out the same colours on the CPU for the painted dirt skin, so keep the two alike).
Shader "Epacenur/Ground"
{
    Properties
    {
        _GrassTex ("Grass", 2D) = "white" {}
        _EarthTex ("Earth", 2D) = "white" {}
        _RockTex ("Rock", 2D) = "white" {}
        _SnowTex ("Snow", 2D) = "white" {}
        _EarthTint ("Earth tint", Color) = (0.5,0.4,0.28,1)
        _RockTint ("Rock tint", Color) = (0.55,0.54,0.5,1)
        _SnowTint ("Snow tint", Color) = (1,1,1,1)
        _Tiles ("Texture repeats across a chunk", Float) = 4
        _Detail ("Detail", Float) = 0.5
        _Edge ("Edge", Float) = 0.2
        _Glossiness ("Smoothness", Range(0,1)) = 0.05
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 200

        CGPROGRAM
        #pragma surface surf Standard vertex:vert fullforwardshadows
        #pragma target 3.0

        sampler2D _GrassTex, _EarthTex, _RockTex, _SnowTex;
        fixed4 _EarthTint, _RockTint, _SnowTint;
        float _Tiles, _Detail, _Edge;
        half _Glossiness;

        struct Input
        {
            float2 spot;
            float4 grassTint;
            float3 cover;
        };

        void vert(inout appdata_full v, out Input o)
        {
            UNITY_INITIALIZE_OUTPUT(Input, o);
            o.spot = v.texcoord.xy * _Tiles;
            o.grassTint = v.color;
            o.cover = float3(v.texcoord1.xy, v.texcoord2.x);
        }

        void surf(Input IN, inout SurfaceOutputStandard o)
        {
            fixed3 g = tex2D(_GrassTex, IN.spot).rgb, e = tex2D(_EarthTex, IN.spot).rgb, r = tex2D(_RockTex, IN.spot).rgb, s = tex2D(_SnowTex, IN.spot).rgb;
            float we = IN.cover.x, wr = IN.cover.y, ws = IN.cover.z, wg = saturate(1 - we - wr - ws);
            float4 h = float4(wg + g.r * _Detail, we + e.r * _Detail, wr + r.r * _Detail, ws + s.r * _Detail);
            float top = max(max(h.x, h.y), max(h.z, h.w)) - _Edge;
            float4 b = max(h - top, 0);
            b /= (b.x + b.y + b.z + b.w);
            o.Albedo = b.x * g * IN.grassTint.rgb + b.y * e * _EarthTint.rgb + b.z * r * _RockTint.rgb + b.w * s * _SnowTint.rgb;
            o.Metallic = 0;
            o.Smoothness = _Glossiness;
            o.Alpha = 1;
        }
        ENDCG
    }
    FallBack "Diffuse"
}
