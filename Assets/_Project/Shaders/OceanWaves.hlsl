#ifndef PIRATES_OCEAN_WAVES_INCLUDED
#define PIRATES_OCEAN_WAVES_INCLUDED

// Ondas do mar na GPU: a mesma soma de senoides de WaveSettings.Evaluate (a física do navio usa aquela, na CPU).
// OceanSurface manda cada onda como (kx, kz, amplitude, fase), com fase = kx·ox + kz·oz − ω·t já reduzida a uma volta
// em double na CPU (ox, oz = onde a malha está): aqui entra só a posição local da malha, sem números grandes
// (o seno da GPU perde precisão longe de zero, e o tempo de jogo só cresce).

#define OCEAN_MAX_WAVES 8

float4 _OceanWaves[OCEAN_MAX_WAVES];
float _OceanWaveCount;
float _OceanSeaLevel;
float _OceanSpacing;

float OceanHeight(float2 p)
{
    float h = _OceanSeaLevel;
    int count = (int)_OceanWaveCount;
    [unroll]
    for (int i = 0; i < OCEAN_MAX_WAVES; i++)
    {
        if (i < count)
        {
            float4 w = _OceanWaves[i];
            h += w.z * sin(w.x * p.x + w.y * p.y + w.w);
        }
    }
    return h;
}

// Sobe o vértice na altura da onda e dá a normal pela inclinação da própria grade (diferença central, como o
// RecalculateNormals da malha na CPU dava). Espaço do objeto: a malha só anda com a câmera, não gira nem escala.
void OceanDisplace(inout float3 positionOS, out float3 normalOS)
{
    float2 p = positionOS.xz;
    float s = max(_OceanSpacing, 0.01);
    positionOS.y = OceanHeight(p);
    float dx = (OceanHeight(p + float2(s, 0)) - OceanHeight(p - float2(s, 0))) / (2 * s);
    float dz = (OceanHeight(p + float2(0, s)) - OceanHeight(p - float2(0, s))) / (2 * s);
    normalOS = normalize(float3(-dx, 1, -dz));
}

#endif
