"""PD の MIDI（Mutopia Project・Public Domain 指定のもの）をオルゴール風に自前レンダリングして OGG にする。
演奏の録音を使わないので、できた音源は本リポジトリのライセンスで同梱できる。
  pip install mido soundfile numpy（＋ffmpeg）
  python Docs/gen_bgm.py <in.mid> <out.ogg> [tempo_scale]
"""
import os, subprocess, sys
import numpy as np, mido, soundfile as sf

SR = 44100

def render(mid_path, tempo_scale=1.0):
    mid = mido.MidiFile(mid_path)
    notes, t, on = [], 0.0, {}
    for msg in mid:                      # mido がテンポ込みで秒へ直してくれる
        t += msg.time * tempo_scale
        if msg.type == 'note_on' and msg.velocity > 0:
            on[(msg.channel, msg.note)] = (t, msg.velocity)
        elif msg.type in ('note_off', 'note_on'):
            on.pop((msg.channel, msg.note), None)   # オルゴールは打ちっぱなし＝音価は使わない
        if msg.type == 'note_on' and msg.velocity > 0:
            notes.append((t, msg.note, msg.velocity))
    out = np.zeros(int((t + 6) * SR), dtype=np.float32)
    for start, note, vel in notes:
        note += 12                       # 櫛歯の音域へ1オクターブ上げる
        f = 440.0 * 2 ** ((note - 69) / 12)
        dur = float(np.clip(3.5 * (440.0 / f) ** 0.5, 1.2, 5.0))   # 低音ほど長く鳴る
        n = int(dur * SR)
        x = np.arange(n) / SR
        # 片持ち梁の非整数倍音（1 : 6.27 は高すぎるので弱く）＋打鍵の瞬間だけ明るく
        tone = (np.sin(2 * np.pi * f * x) * np.exp(-x * 3.0 / dur)
                + 0.25 * np.sin(2 * np.pi * f * 2.0 * x) * np.exp(-x * 8.0 / dur)
                + 0.08 * np.sin(2 * np.pi * f * 6.27 * x) * np.exp(-x * 30.0 / dur))
        tone *= np.minimum(1.0, x / 0.002)          # クリック防止の2msアタック
        i = int(start * SR)
        out[i:i + n] += (tone * (vel / 127.0) ** 1.5).astype(np.float32)[: len(out) - i]
    out /= max(1e-9, np.abs(out).max()) / 0.5        # -6dBFS。音量はUnity側のノブで絞る
    return out

if __name__ == '__main__':
    src, dst = sys.argv[1], sys.argv[2]
    audio = render(src, float(sys.argv[3]) if len(sys.argv) > 3 else 1.0)
    assert np.isfinite(audio).all() and 0.4 < np.abs(audio).max() <= 0.5 and len(audio) > SR * 30
    # libsndfile の Vorbis 書き出しは長尺で落ちる版があるので、WAV → ffmpeg で OGG にする
    wav = dst + '.wav'
    sf.write(wav, audio, SR, subtype='PCM_16')
    subprocess.run(['ffmpeg', '-y', '-loglevel', 'error', '-i', wav, '-c:a', 'libvorbis', '-q:a', '4', dst], check=True)
    os.remove(wav)
    print(dst, f'{len(audio) / SR:.1f}s')
