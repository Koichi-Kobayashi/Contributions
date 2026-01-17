# GitHub Contributions Chart Generator - Desktop Edition

GitHubのContributionsチャートを取得して表示し、画像としてクリップボードにコピーできるデスクトップアプリケーションです。

## 機能

- GitHubのURLまたはユーザー名からContributionsデータを取得
- 複数のテーマから選択可能
- 生成されたチャートをクリップボードにコピー（Xなどでシェア可能）

## セットアップ

### 必要な環境

- Node.js 16以上
- npm または yarn

### インストール

```bash
npm install
```

## 開発

開発モードで起動：

```bash
npm run electron:dev
```

## ビルド

デスクトップアプリとしてビルド：

```bash
npm run electron:build
```

ビルドされたアプリは `release` ディレクトリに出力されます。

## 使い方

1. アプリを起動
2. GitHubのURL（例: `https://github.com/username`）またはユーザー名を入力
3. 「生成！」ボタンをクリック
4. チャートが表示されたら「クリップボードにコピー」ボタンをクリック
5. Xなどで画像をシェア

## 技術スタック

- Electron
- React
- Vite
- github-contributions-canvas

## ライセンス

MIT
