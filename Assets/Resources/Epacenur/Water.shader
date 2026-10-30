// Ponds and rivers: ripples (_MainTex) that drift along each river the way it runs, and lie almost still on
// ponds. The flow (tiles a second, from the overworld) is in each vertex's second uv; ripples are laid out by
// the vertex's own tile position (the map's local space, which doesn't move when the origin does). Two copies
// of the ripples, half a cycle apart, take turns so the drift never stretches them.
//
// The water is see-through, by how deep it is (each vertex colour's red: 0 to 1 of the overworld's water
// depth, worked out from the land when the mesh is made): clear and pale in the shallows, so the sandy bed
// shows, deeper and bluer further out, with a broken line of foam just in from the shore. Its outline (the
// first uv's x: 0 on the edge, 1 a little way in) fades it out softly at its edges; the first uv's y (0 on
// the edge, 1 about a tile in) keeps it shallow near the shore and places the foam.
Shader "Epacenur/Water"
{
    Properties
    {
        _Color ("Color", Color) = (0.2,0.42,0.7,1)
        _MainTex ("Ripples", 2D) = "white" {}
        _Scale ("Tiles per ripple texture", Float) = 4
        _Cycle ("Seconds per cycle", Float) = 1.5
        _Glossiness ("Smoothness", Range(0,1)) = 0.8
        _Clear ("Depth that's fully coloured (of the full depth)", Float) = 0.8
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        LOD 200
        ZWrite Off

        CGPROGRAM
        #pragma surface surf Standard vertex:vert alpha:fade
        #pragma target 3.0

        sampler2D _MainTex;
        fixed4 _Color;
        float _Scale;
        float _Cycle;
        half _Glossiness;
        float _Clear;

        struct Input
        {
            float2 flow;
            float2 spot;
            float2 edge;
            float4 color : COLOR;
        };

        void vert(inout appdata_full v, out Input o)
        {
            UNITY_INITIALIZE_OUTPUT(Input, o);
            o.flow = v.texcoord1.xy;
            o.spot = v.vertex.xz;
            o.edge = v.texcoord.xy;
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

            // How deep: shallow by the shore, whatever the bed does.
            float body = saturate(IN.color.r / _Clear) * (0.35 + 0.65 * IN.edge.y);

            float3 shallow = lerp(_Color.rgb, float3(0.55, 0.82, 0.85), 0.5);
            float3 deep = _Color.rgb * 0.6;
            float3 col = lerp(shallow, deep, body) * (0.82 + 0.36 * r);
            // Foam: a broken line just in from the shore.
            float foam = smoothstep(0.03, 0.12, IN.edge.y) * (1 - smoothstep(0.16, 0.32, IN.edge.y));
            foam *= smoothstep(0.45, 0.7, r + foam * 0.3);
            col = lerp(col, float3(0.93, 0.96, 0.97), foam * 0.7);

            o.Albedo = col;
            o.Metallic = 0;
            o.Smoothness = _Glossiness;
            o.Alpha = saturate(max(lerp(0.35, 0.88, body), foam * 0.8) * saturate(IN.edge.x));
        }
        ENDCG
    }
    FallBack "Diffuse"
}
