#ifndef PIRATES_OCEAN_SHORE_INCLUDED
#define PIRATES_OCEAN_SHORE_INCLUDED

// Cor do mar (só o passo ForwardLit). Mar aberto: crista da onda mais clara e espuma recortada no alto das cristas
// (pela altura da água contra a soma das amplitudes). Costa: água turquesa perto das ilhas, mais clara no raso, e espuma
// na linha d'água e em faixas que chegam na praia. Sem textura de profundidade e sem transparência (a água continua opaca: o O5 cortou da
// ilha desenhada o que fica abaixo de −2 m). Tudo vem de um mapa por cena feito pelo gerador (R = altura do chão,
// G = distância até o litoral) e de um ruído pequeno que repete: 1 amostra (o ruído) fora do mapa, 2 dentro.
// Cores e medidas são globais mandadas pelo OceanSurface (o material continua o do URP Lit, compatível com o SRP Batcher).

TEXTURE2D(_OceanShoreMap);   SAMPLER(sampler_OceanShoreMap);
TEXTURE2D(_OceanFoamNoise);  SAMPLER(sampler_OceanFoamNoise);
float4 _OceanShoreRect;      // origem x, origem z, 1/largura, 1/comprimento (z = 0: cena sem mapa)
float4 _OceanShoreRange;     // altura mínima, faixa de altura, distância máxima (m), nível do mar
half4  _OceanShallowColor;   // turquesa perto da costa (a = força)
half4  _OceanShoalColor;     // raso de verdade, areia aparecendo (a = força)
half4  _OceanFoamColor;      // espuma (a = força)
float4 _OceanShoreParams;    // largura do turquesa (m), profundidade da espuma na linha d'água (m), escala do ruído, alcance das faixas (m)
float4 _OceanFoamWaves;      // faixas de espuma: metros entre faixas, velocidade (faixas/s), espessura (0–1), 0
float4 _OceanCrest;          // soma das amplitudes (m), início da cor da crista e da espuma (fração da altura máxima), força da espuma
half4  _OceanCrestColor;     // crista (a = força)

// Espuma por cima de tudo (a cor da costa não tinge a espuma das cristas).
void OceanApplyFoam(inout SurfaceData surface, half foam)
{
    surface.albedo = lerp(surface.albedo, _OceanFoamColor.rgb, foam);
    surface.smoothness = lerp(surface.smoothness, 0.15, foam);
}

void OceanShore(float3 positionWS, inout SurfaceData surface)
{
    // Ruído no topo, antes de qualquer desvio por pixel: as derivadas implícitas valem (mip certo) sem _GRAD, que é bem
    // mais lento na GPU Intel. A mesma amostra serve para as cristas e para a costa.
    float t = _Time.y;
    float2 noiseUV = positionWS.xz * _OceanShoreParams.z + float2(t * 0.013, t * 0.009);
    // 0,1–0,95: sem espuma pedida não sai mancha nenhuma, e com espuma total ela fecha.
    half noise = 0.1 + 0.85 * SAMPLE_TEXTURE2D(_OceanFoamNoise, sampler_OceanFoamNoise, noiseUV).r;

    // Mar aberto: quanto da altura máxima a água está agora (0 no nível do mar, 1 com todas as ondas somando).
    half crest = saturate((positionWS.y - _OceanSeaLevel) / max(_OceanCrest.x, 0.01));
    surface.albedo = lerp(surface.albedo, _OceanCrestColor.rgb, smoothstep(_OceanCrest.y, 1, crest) * _OceanCrestColor.a);
    half caps = smoothstep(_OceanCrest.z, 1, crest) * _OceanCrest.w;
    half crestFoam = smoothstep(noise - 0.04, noise + 0.04, caps) * _OceanFoamColor.a;

    float2 uv = (positionWS.xz - _OceanShoreRect.xy) * _OceanShoreRect.zw;
    if (_OceanShoreRect.z <= 0 || any(uv < 0) || any(uv > 1))
    {
        OceanApplyFoam(surface, crestFoam);
        return;
    }

    // Mapa sem mip: nível 0 explícito (dentro de um desvio por pixel não há derivada).
    half2 map = SAMPLE_TEXTURE2D_LOD(_OceanShoreMap, sampler_OceanShoreMap, uv, 0).rg;
    float ground = _OceanShoreRange.x + map.r * _OceanShoreRange.y;
    float coastDistance = map.g * _OceanShoreRange.z;
    // Profundidade na hora: a onda sobe e desce, a linha de espuma vai e volta na praia.
    float depth = positionWS.y - ground;

    half halo = 1 - smoothstep(0, _OceanShoreParams.x, coastDistance);
    half shoal = 1 - saturate(depth / 2.5);
    surface.albedo = lerp(surface.albedo, _OceanShallowColor.rgb, halo * halo * _OceanShallowColor.a);
    surface.albedo = lerp(surface.albedo, _OceanShoalColor.rgb, shoal * shoal * _OceanShoalColor.a);

    // Linha d'água: espuma onde a água é mais rasa que _OceanShoreParams.y.
    half edge = 1 - saturate(depth / _OceanShoreParams.y);
    // Faixas que andam em direção à praia (fase cai com a distância) e somem longe dela.
    half wave = frac(coastDistance / _OceanFoamWaves.x + t * _OceanFoamWaves.y);
    half band = smoothstep(1 - _OceanFoamWaves.z, 1, wave) * (1 - saturate(coastDistance / _OceanShoreParams.w));
    half amount = saturate(edge + band * 0.75);
    // O ruído recorta a espuma em manchas (borda dura, do jeito low-poly), mais cheia perto da linha d'água.
    half shoreFoam = smoothstep(noise - 0.04, noise + 0.04, amount) * _OceanFoamColor.a;

    OceanApplyFoam(surface, max(crestFoam, shoreFoam));
}

#endif
