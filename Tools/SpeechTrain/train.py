"""Обучить модель на три слова и выгрузить её в ONNX.

  .venv\\Scripts\\python train.py [--epochs 40]

Вход модели — 16 эмбеддингов openWakeWord (≈2 с звука, по 96 чисел), выход — вероятности четырёх классов:
0 «не команда», 1 verity, 2 lovety, 3 moggaty. Учится на data/train.npz, проверяется на data/val.npz (там голоса,
которых нет в обучении). В конце — таблица: сколько слов узнано и сколько раз сработало зря при разных порогах.

Результат: models/ssdfnc_words.onnx (вход «embeddings» [n, 16, 96], выход «probs» [n, 4]).
"""
import argparse
import os
import sys

import numpy as np
import torch
from torch import nn

import texts

HERE = os.path.dirname(os.path.abspath(__file__))
DATA = os.path.join(HERE, "data")
MODELS = os.path.join(HERE, "models")
NAMES = ["—"] + [w.upper() for w in texts.WORDS]


class Net(nn.Module):
    def __init__(self, hidden=128):
        super().__init__()
        self.body = nn.Sequential(
            nn.Flatten(),
            nn.Linear(16 * 96, hidden), nn.LayerNorm(hidden), nn.ReLU(), nn.Dropout(0.25),
            nn.Linear(hidden, hidden), nn.LayerNorm(hidden), nn.ReLU(), nn.Dropout(0.25),
            nn.Linear(hidden, 4))

    def forward(self, x):
        return self.body(x)


class Exported(nn.Module):
    """То, что уходит в ONNX: сразу вероятности"""

    def __init__(self, net):
        super().__init__()
        self.net = net

    def forward(self, embeddings):
        return torch.softmax(self.net(embeddings), dim=-1)


def load(split):
    d = np.load(os.path.join(DATA, f"{split}.npz"))
    return torch.tensor(d["X"].astype(np.float32)), torch.tensor(d["y"]), d["kinds"]


def report(probs, y, kinds):
    """Узнано / ложных срабатываний при разных порогах"""
    pred = probs.argmax(1)
    top = probs.max(1).values
    neg_kinds = [k for k in ("trap", "speech", "late", "cut") if (kinds == k).any()]
    print("порог | " + " | ".join(f"{n} узнано" for n in NAMES[1:]) + " | ложных на «не команде» | "
          + " | ".join(f"из них {k}" for k in neg_kinds) + " | путает слова")
    for thr in (0.5, 0.7, 0.8, 0.9, 0.95):
        fire = (top >= thr) & (pred > 0)
        recall = [((fire & (pred == c) & (y == c)).sum() / max(1, (y == c).sum())).item() for c in (1, 2, 3)]
        neg = y == 0
        fp = (fire & neg).sum().item() / max(1, neg.sum().item())
        fp_kind = []
        for k in neg_kinds:
            m = torch.tensor(kinds == k)
            fp_kind.append((fire & m).sum().item() / max(1, m.sum().item()))
        confuse = (fire & (y > 0) & (pred != y)).sum().item() / max(1, (y > 0).sum().item())
        print(f"{thr:.2f}  | " + " | ".join(f"{r * 100:5.1f}%" for r in recall) + f" | {fp * 100:.2f}% | "
              + " | ".join(f"{f * 100:.2f}%" for f in fp_kind) + f" | {confuse * 100:.2f}%")


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--epochs", type=int, default=40)
    ap.add_argument("--batch", type=int, default=512)
    ap.add_argument("--lr", type=float, default=1e-3)
    ap.add_argument("--neg-weight", type=float, default=2.0, help="вес «не команды»: больше — реже ложные срабатывания")
    args = ap.parse_args()
    torch.manual_seed(1)

    Xtr, ytr, _ = load("train")
    Xva, yva, kva = load("val")
    print(f"обучение {tuple(Xtr.shape)} классы {torch.bincount(ytr).tolist()}, проверка {tuple(Xva.shape)}")
    net = Net()
    opt = torch.optim.AdamW(net.parameters(), lr=args.lr, weight_decay=1e-3)
    sched = torch.optim.lr_scheduler.CosineAnnealingLR(opt, args.epochs)
    loss_fn = nn.CrossEntropyLoss(weight=torch.tensor([args.neg_weight, 1.0, 1.0, 1.0]), label_smoothing=0.05)

    for epoch in range(args.epochs):
        net.train()
        perm = torch.randperm(len(Xtr))
        total = 0.0
        for i in range(0, len(perm), args.batch):
            idx = perm[i:i + args.batch]
            x = Xtr[idx] + 0.05 * torch.randn_like(Xtr[idx])  # немного шума в признаки
            loss = loss_fn(net(x), ytr[idx])
            opt.zero_grad()
            loss.backward()
            opt.step()
            total += loss.item() * len(idx)
        sched.step()
        if epoch % 5 == 4 or epoch == args.epochs - 1:
            net.eval()
            with torch.no_grad():
                acc = (net(Xva).argmax(1) == yva).float().mean().item()
            print(f"эпоха {epoch + 1}: потери {total / len(Xtr):.4f}, точность на проверке {acc * 100:.1f}%")

    net.eval()
    with torch.no_grad():
        probs = torch.softmax(net(Xva), 1)
    print("\nПроверка (голоса, которых не было в обучении):")
    report(probs, yva, kva)

    os.makedirs(MODELS, exist_ok=True)
    path = os.path.join(MODELS, "ssdfnc_words.onnx")
    torch.onnx.export(Exported(net), torch.zeros(1, 16, 96), path, input_names=["embeddings"], output_names=["probs"],
                      dynamic_axes={"embeddings": {0: "n"}, "probs": {0: "n"}}, opset_version=17, dynamo=False)
    print(f"\nмодель: {path} ({os.path.getsize(path) / 1024:.0f} КБ)")


if __name__ == "__main__":
    sys.exit(main())
