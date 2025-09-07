Shader "Custom/InstancedSpheres"
{
    Properties
    {
        _BaseColor("Base Color", Color) = (1,1,1,1)
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct Data
            {
                float4 color;
                float3 position;
                int count;
            };

            StructuredBuffer<Data> _InstanceData;
            float _Scale = 0.1;

            struct appdata
            {
                float3 vertex : POSITION;
                uint instanceID : SV_InstanceID;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float4 color : COLOR;
            };


            v2f vert(appdata v)
            {
                v2f o;
                Data d = _InstanceData[v.instanceID];
                float3 scaledPos = v.vertex * d.count * _Scale + d.position;
                o.pos = UnityObjectToClipPos(float4(scaledPos,1.0));
                o.color = d.color;
                return o;
            }

            half4 frag(v2f i) : SV_Target
            {
                return i.color;
            }
            ENDHLSL
        }
    }
}
