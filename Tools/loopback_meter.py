# Records the default output device (WASAPI loopback) and prints, once per second, the 440 Hz level
# and total RMS, labelled with the latest [S05sweep] line from the client's log.
import sys, time, math, re
import numpy as np
import pyaudiowpatch as pa

secs = float(sys.argv[1])
logpath = sys.argv[2] if len(sys.argv) > 2 else None

p = pa.PyAudio()
dev = p.get_default_wasapi_loopback()
rate = int(dev["defaultSampleRate"]); ch = dev["maxInputChannels"]
print(f"device: {dev['name']} {rate} Hz {ch} ch", flush=True)
s = p.open(format=pa.paFloat32, channels=ch, rate=rate, input=True, input_device_index=dev["index"], frames_per_buffer=rate // 10)

label = "-"
start = time.time()
buf = []
while time.time() - start < secs:
    # Loopback delivers nothing while the device is silent (read would block); count that as zeros.
    time.sleep(0.1)
    avail = s.get_read_available()
    if avail:
        a = np.frombuffer(s.read(avail, exception_on_overflow=False), dtype=np.float32)
        buf.append(a.reshape(-1, ch)[:, 0])
    else:
        buf.append(np.zeros(rate // 10, np.float32))
    if sum(len(b) for b in buf) >= rate:
        x = np.concatenate(buf)[:rate]; buf = []
        spec = np.abs(np.fft.rfft(x)) ** 2
        f = np.fft.rfftfreq(len(x), 1 / rate)
        tone = math.sqrt(spec[(f > 420) & (f < 460)].sum() * 2) / len(x)  # amplitude in the 440 Hz band
        if logpath:
            try:
                lines = [l for l in open(logpath, encoding="utf-8", errors="ignore") if "[S05sweep]" in l]
                if lines: label = lines[-1].strip()
            except OSError:
                pass
        print(f"{time.time() - start:5.1f}s  440Hz amp {tone:.4f}  rms {np.sqrt(np.mean(x * x)):.4f}  {label}", flush=True)
