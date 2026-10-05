using UnityEngine;

namespace Pirates.VFX
{
    /// <summary>
    /// Um ParticleSystem em espaço de mundo alimentado só por <see cref="Emit"/> (nada é instanciado por efeito): cada tipo
    /// de partícula (fumaça, clarão, faísca, água, espuma, fogo, lascas) é um destes, montado em código. O comportamento
    /// comum fica no sistema (gravidade, crescimento, arrasto, vento, giro); posição, velocidade, tamanho, vida e cor vêm
    /// de cada emissão.
    /// </summary>
    sealed class FxSystem
    {
        public struct Style
        {
            public float gravity;
            /// <summary>Tamanho no fim da vida em relação ao início (fumaça cresce, clarão não).</summary>
            public float growth;
            /// <summary>Arrasto do ar/água (a fumaça para logo depois de sair do canhão).</summary>
            public float drag;
            public Vector3 wind;
            public ParticleSystemRenderMode mode;
            /// <summary>Esticada pela velocidade (faíscas).</summary>
            public float stretch;
            public Mesh mesh;
            public int maxParticles;
            /// <summary>Menor = desenhado por cima dos outros sistemas transparentes.</summary>
            public float sortingFudge;
            /// <summary>Fração da vida em que some (1 = vai sumindo a vida toda).</summary>
            public float fadeStart;
            /// <summary>Em pé (chamas): só uma inclinação pequena, em vez de girar à toa.</summary>
            public bool upright;
        }

        readonly ParticleSystem system;
        readonly bool mesh3D, upright;

        public FxSystem(Transform parent, string name, Material material, Style style)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            system = go.AddComponent<ParticleSystem>();
            system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            mesh3D = style.mesh;
            upright = style.upright;

            var main = system.main;
            main.loop = true;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.gravityModifier = style.gravity;
            main.maxParticles = style.maxParticles > 0 ? style.maxParticles : 800;
            main.startRotation3D = mesh3D;
            main.startSize3D = mesh3D;
            main.scalingMode = ParticleSystemScalingMode.Shape;

            var emission = system.emission;
            emission.enabled = false;
            var shape = system.shape;
            shape.enabled = false;

            var color = system.colorOverLifetime;
            color.enabled = true;
            var fade = new Gradient();
            float fadeStart = Mathf.Clamp(style.fadeStart, 0.02f, 0.95f);
            fade.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.02f), new GradientAlphaKey(1f, fadeStart), new GradientAlphaKey(0f, 1f) });
            color.color = fade;

            if (style.growth > 0f && !Mathf.Approximately(style.growth, 1f))
            {
                var size = system.sizeOverLifetime;
                size.enabled = true;
                float g = style.growth;
                // Cresce rápido no começo e devagar no fim (fumaça se abrindo).
                var curve = g > 1f
                    ? new AnimationCurve(new Keyframe(0f, 1f / g), new Keyframe(0.3f, (1f + (g - 1f) * 0.6f) / g), new Keyframe(1f, 1f))
                    : new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f, g));
                size.size = new ParticleSystem.MinMaxCurve(g > 1f ? g : 1f, curve);
            }

            if (style.drag > 0f)
            {
                var limit = system.limitVelocityOverLifetime;
                limit.enabled = true;
                limit.limit = 1000f;
                limit.drag = style.drag;
                limit.multiplyDragByParticleSize = false;
                limit.multiplyDragByParticleVelocity = true;
            }

            if (style.wind != Vector3.zero)
            {
                var force = system.forceOverLifetime;
                force.enabled = true;
                force.space = ParticleSystemSimulationSpace.World;
                force.x = style.wind.x;
                force.y = style.wind.y;
                force.z = style.wind.z;
            }

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = style.mesh ? ParticleSystemRenderMode.Mesh : style.mode;
            if (style.mesh) renderer.mesh = style.mesh;
            if (style.stretch > 0f)
            {
                renderer.renderMode = ParticleSystemRenderMode.Stretch;
                renderer.velocityScale = style.stretch;
                renderer.lengthScale = 1f;
            }
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.sortingFudge = style.sortingFudge;
            renderer.alignment = ParticleSystemRenderSpace.View;
            if (material) renderer.sharedMaterial = material;

            system.Play();
        }

        /// <summary>Uma partícula. <paramref name="spin"/> em graus por segundo (2D) ou um eixo aleatório (lascas).</summary>
        public void Emit(Vector3 position, Vector3 velocity, float size, float lifetime, Color color, float spin = 0f)
        {
            var p = new ParticleSystem.EmitParams
            {
                position = position,
                velocity = velocity,
                startSize = size,
                startLifetime = lifetime,
                startColor = color,
                applyShapeToPosition = false,
            };
            if (mesh3D)
            {
                p.rotation3D = new Vector3(Random.value * 360f, Random.value * 360f, Random.value * 360f);
                p.angularVelocity3D = Random.insideUnitSphere * spin;
            }
            else
            {
                p.rotation = upright ? Random.Range(-12f, 12f) : Random.value * 360f;
                p.angularVelocity = spin;
            }
            system.Emit(p, 1);
        }

        /// <summary>Lasca: tamanho por eixo (comprida e fina).</summary>
        public void EmitSized(Vector3 position, Vector3 velocity, Vector3 size, float lifetime, Color color, float spin)
        {
            var p = new ParticleSystem.EmitParams
            {
                position = position,
                velocity = velocity,
                startSize3D = size,
                startLifetime = lifetime,
                startColor = color,
                applyShapeToPosition = false,
                rotation3D = new Vector3(Random.value * 360f, Random.value * 360f, Random.value * 360f),
                angularVelocity3D = Random.insideUnitSphere * spin,
            };
            system.Emit(p, 1);
        }

        public int Count => system.particleCount;
    }
}
