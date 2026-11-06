"""Признаки звука для своей модели: мел-спектр → эмбеддинги по 96 чисел на каждые 80 мс.

Модели признаков — из openWakeWord (features/melspectrogram.onnx, features/embedding_model.onnx), шаги — как у
него: мел/10 + 2, окно эмбеддинга 76 кадров мел-спектра с шагом 8. Нужны только numpy и onnxruntime — поэтому
это же работает и в окружении Tools/SpeechTest, и так же будет считать игра.
"""
import os

import numpy as np

HERE = os.path.dirname(os.path.abspath(__file__))
FEATURES = os.path.join(HERE, "features")
RATE = 16000
CLIP = int(2.0 * RATE)  # пример модели: 2 с звука → 16 эмбеддингов
FRAMES = 16


class Features:
    def __init__(self, threads=4):
        import onnxruntime as ort
        opts = ort.SessionOptions()
        opts.intra_op_num_threads = threads
        self.mel = ort.InferenceSession(os.path.join(FEATURES, "melspectrogram.onnx"), opts, providers=["CPUExecutionProvider"])
        self.emb = ort.InferenceSession(os.path.join(FEATURES, "embedding_model.onnx"), opts, providers=["CPUExecutionProvider"])

    def embed(self, clips):
        """int16 (n, CLIP) → (n, FRAMES, 96): последние FRAMES эмбеддингов"""
        x = clips.astype(np.float32)
        spec = self.mel.run(None, {self.mel.get_inputs()[0].name: x})[0]   # (n, 1, frames, 32)
        spec = spec[:, 0] / 10 + 2
        n, frames, _ = spec.shape
        starts = list(range(0, frames - 76 + 1, 8))[-FRAMES:]
        win = np.stack([spec[:, s:s + 76] for s in starts], axis=1)          # (n, FRAMES, 76, 32)
        out = self.emb.run(None, {self.emb.get_inputs()[0].name: win.reshape(-1, 76, 32, 1).astype(np.float32)})[0]
        return out.reshape(n, len(starts), 96)
