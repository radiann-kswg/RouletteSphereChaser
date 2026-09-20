"""物理音の素材を手続き生成する（すべて自作＝本リポジトリのライセンスで同梱可）。
  pip install numpy soundfile
  python Docs/gen_sfx.py            # リポジトリ直下で実行 → Assets/Resources/SFX/*.wav
音色を変えたいときはここの定数を直して再生成する。音量・ピッチ幅は Unity 側（BallAudio）のノブ。
"""
import os
import numpy as np, soundfile as sf

SR = 44100
OUT = 'Assets/Resources/SFX'   # ParkAudio が Resources.Load で拾う（シーン配線なし）
rng = np.random.default_rng(80)          # 再生成しても同じ波形になるよう固定

def loop_noise(seconds, lo, hi, tilt=1.0):
    """周波数領域で作る＝継ぎ目なしでループする帯域ノイズ"""
    n = int(seconds * SR)
    f = np.fft.rfftfreq(n, 1 / SR)
    mag = ((f > lo) & (f < hi)) / np.maximum(f, 1.0) ** tilt
    x = np.fft.irfft(mag * np.exp(2j * np.pi * rng.random(len(f))), n)
    return x / np.abs(x).max()

def save(name, x, peak=0.7):
    x = x / np.abs(x).max() * peak
    assert np.isfinite(x).all()
    sf.write(os.path.join(OUT, name), x.astype(np.float32), SR, subtype='PCM_16')
    print(name, f'{len(x) / SR:.2f}s')

os.makedirs(OUT + '/Hits', exist_ok=True)

# 転がりの連続ループは作らない（User 判断 2026-09-20）: 衝突音と同時に鳴ると主張が強い。
# 転がりは BallAudio が下の Hit をごく小さく、進んだ距離ごとに刻んで表現する。

# 衝突: プラスチック同士の「コトッ」。金属に聞こえる原因は「長く鳴る正弦＋非整数倍音」なので、
# 倍音は足さず、基音も 10ms 台で消す。主成分は低域寄りに丸めたノイズの一打ち。高さ違いを4種
for i, f0 in enumerate([620, 760, 900, 1080]):
    t = np.arange(int(0.06 * SR)) / SR
    body = np.sin(2 * np.pi * f0 * t) * np.exp(-t * 260)
    thud = np.convolve(rng.standard_normal(len(t)), np.hanning(64) / 32, 'same') * np.exp(-t * 330)   # 約1.4kHz以下へ丸める
    save(f'Hits/Hit_{i + 1}.wav', (body + 0.8 * thud) * np.minimum(1, t / 0.0015), peak=0.6)

# リフト: 低いモーターのうなり。基音＋倍音＋わずかな機械ノイズ。全成分を2秒の整数周期に載せてループさせる
t = np.arange(int(2.0 * SR)) / SR
lift = sum(a * np.sin(2 * np.pi * f * t) for f, a in [(55, 1.0), (110, 0.5), (165, 0.25), (220, 0.12)]) \
     * (0.85 + 0.15 * np.sin(2 * np.pi * 6 * t)) + 0.15 * loop_noise(2.0, 200, 1200)
save('Lift_Loop.wav', lift, peak=0.5)
