# Template project for Blazor WebAssembly on AWS S3

[English](README.md) | 日本語

[Template](https://usausa.github.io/template/) project for Blazor WebAssembly on AWS S3.

# ✅ 実装

| 分類 | 機能 |
| --- | --- |
| 認証 | Cognito Managed Login(OIDC code + PKCE) / Identity Pool(ユーザーごとの S3 参照) / API Gateway JWT オーソライザー / 管理者が発行するユーザー |
| データ | S3 のユーザーごとのファイル(`users/{sub}/`) / CSV 表示(集計値・グラフ・明細) |
| API | API Gateway HTTP API / Lambda(Annotations) |
| 配信 | S3 + CloudFront(OAC・セキュリティヘッダー・SPA フォールバック) |
| インフラ | AWS CDK(C#) / 構築・更新・削除スクリプト(PowerShell) |

# 📚 ドキュメント

| ドキュメント | 内容 |
| --- | --- |
| [architecture.md](docs/architecture.md) | 設計(前提と割り切り・AWS リソース・認証と認可・パフォーマンス・セキュリティ) |
| [setup.md](docs/setup.md) | 新規プロジェクト・セットアップ・デプロイ・削除・プログラム構成・scripts・手動手順・ローカル開発・拡張 |
| [notes.md](docs/notes.md) | 実装上の落とし穴・ランニングコスト・既知の制約 |

# 🔗 関連プロジェクト

| プロジェクト | 説明 |
| --- | --- |
| [template-aws-lambda](https://github.com/usausa/template-aws-lambda) | AWS Lambda + API Gateway のテンプレート |
| [template-aws-api-tenant](https://github.com/usausa/template-aws-api-tenant) | AWS マルチテナントサーバーレス API のテンプレート |
| [template-aws-line-miniapp](https://github.com/usausa/template-aws-line-miniapp) | AWS で動かす LINE ミニアプリ(LIFF)のテンプレート |
| [template-azure-functions](https://github.com/usausa/template-azure-functions) | Azure Functions(isolated worker)のテンプレート |

# 📦 自作ライブラリ

| ライブラリ | 説明 |
| --- | --- |
| [Usa.Smart.Mapper](https://github.com/usausa/Smart-Net-Mapper) | オブジェクトマッパー |
