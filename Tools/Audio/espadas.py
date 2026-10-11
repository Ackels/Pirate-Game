# Tinidos de lâmina (pacote A, passo 6, 09/10; pedido do Jhonata: "não parece espadas se batendo"). Sintetizados, sem bibliotecas:
#   estalo (ruído de 3–5 ms, agudo) + tinido (parciais inarmônicos de barra metálica 1 / 2,76 / 5,40 / 8,93 × fundamental, cada um em
#   dois senos desafinados ±0,25% para o brilho trêmulo, os agudos morrendo antes) + raspão (ruído passa-faixa descendo, a lâmina
#   deslizando) + corpo (baque grave curto).
#   Defesa = tinido curto e mais grave; Aparar = mais longo, mais agudo, mais alto e com raspão maior.
# Saída: Assets/_Project/Audio/Espadas/Defesa_1..5.wav e Aparar_1..4.wav (mono, 48 kHz, 16 bits). Rodar de novo refaz os mesmos arquivos
# (mesmo GUID no Unity). Uso:  python -I Tools/Audio/espadas.py
import math, os, random, struct, wave

RATE = 48000
OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', '..', 'Assets', '_Project', 'Audio', 'Espadas')
RATIOS = [1.0, 2.76, 5.40, 8.93]


def lowpass(x, hz):
    a = 1 - math.exp(-2 * math.pi * hz / RATE)
    y, s = [], 0.0
    for v in x:
        s += a * (v - s)
        y.append(s)
    return y


def highpass(x, hz):
    return [a - b for a, b in zip(x, lowpass(x, hz))]


def rms100(x):
    win = RATE // 10
    best = 0.0
    for s in range(0, max(1, len(x) - win), win // 4):
        best = max(best, sum(v * v for v in x[s:s + win]) / win)
    return 10 * math.log10(max(best, 1e-12))


def clash(seed, f0, tau, amps, seconds, scrape_amp, scrape_ms, scrape_from, scrape_to, body_hz, body_amp, target_db):
    rnd = random.Random(seed)
    n = int(seconds * RATE)
    y = [0.0] * n
    # Tinido.
    for k, (ratio, amp) in enumerate(zip(RATIOS, amps)):
        f = f0 * ratio * rnd.uniform(0.985, 1.015)
        t_k = tau / (1 + 0.7 * k)
        for detune in (0.9975, 1.0025):
            w = 2 * math.pi * f * detune / RATE
            ph = rnd.uniform(0, 2 * math.pi)
            for i in range(n):
                env = math.exp(-i / RATE / t_k)
                if env < 1e-4:
                    break
                y[i] += 0.5 * amp * env * math.sin(w * i + ph)
    # Estalo: ruído agudo de ~4 ms.
    click = highpass([rnd.uniform(-1, 1) for _ in range(int(0.012 * RATE))], 2500)
    for i, v in enumerate(click):
        y[i] += 1.1 * v * math.exp(-i / RATE / 0.0015)
    # Raspão: ruído passa-faixa com o centro descendo de scrape_from a scrape_to Hz.
    m = int(scrape_ms / 1000 * RATE)
    s_lo = s_hi = 0.0
    for i in range(m):
        t = i / m
        centre = scrape_from + (scrape_to - scrape_from) * t
        a_hi = 1 - math.exp(-2 * math.pi * centre * 1.5 / RATE)
        a_lo = 1 - math.exp(-2 * math.pi * centre / 1.5 / RATE)
        v = rnd.uniform(-1, 1)
        s_hi += a_hi * (v - s_hi)
        s_lo += a_lo * (s_hi - s_lo)
        env = min(1.0, t / 0.08) * (1 - t) ** 1.5
        y[i] += scrape_amp * 3.0 * (s_hi - s_lo) * env
    # Corpo: baque grave curto (a força do golpe).
    for i in range(int(0.12 * RATE)):
        t = i / RATE
        y[i] += body_amp * math.sin(2 * math.pi * body_hz * t) * math.exp(-t / 0.035) * min(1.0, t / 0.002)
    # Fim sem estalo, compressão suave até o alvo (janela de 100 ms mais forte).
    fade = int(0.05 * RATE)
    for i in range(fade):
        y[n - 1 - i] *= i / fade
    peak = max(abs(v) for v in y)
    y = [v / peak for v in y]
    lo, hi = 0.3, 4.0
    for _ in range(20):
        g = (lo + hi) / 2
        if rms100([0.95 * math.tanh(g * v) for v in y]) < target_db:
            lo = g
        else:
            hi = g
    return [0.95 * math.tanh(lo * v) for v in y], 20 * math.log10(lo)


def write(name, x):
    os.makedirs(OUT, exist_ok=True)
    w = wave.open(os.path.join(OUT, name + '.wav'), 'wb')
    w.setnchannels(1); w.setsampwidth(2); w.setframerate(RATE)
    w.writeframes(struct.pack('<%dh' % len(x), *[max(-32767, min(32767, int(round(v * 32767)))) for v in x]))
    w.close()


for i, f0 in enumerate([820, 910, 990, 1080, 1160]):
    x, gain = clash(100 + i, f0, tau=0.2, amps=[1.0, 0.7, 0.45, 0.3], seconds=0.6, scrape_amp=0.12, scrape_ms=45,
                    scrape_from=6000, scrape_to=3500, body_hz=190, body_amp=0.45, target_db=-12.0)
    write(f'Defesa_{i + 1}', x)
    print(f'Defesa_{i + 1}: {f0} Hz, ganho {gain:+.1f} dB, RMS {rms100(x):.1f} dB, {len(x) / RATE:.2f} s')
for i, f0 in enumerate([1250, 1380, 1520, 1660]):
    x, gain = clash(200 + i, f0, tau=0.5, amps=[1.0, 0.85, 0.6, 0.45], seconds=1.3, scrape_amp=0.3, scrape_ms=140,
                    scrape_from=7500, scrape_to=3000, body_hz=150, body_amp=0.5, target_db=-9.5)
    write(f'Aparar_{i + 1}', x)
    print(f'Aparar_{i + 1}: {f0} Hz, ganho {gain:+.1f} dB, RMS {rms100(x):.1f} dB, {len(x) / RATE:.2f} s')
