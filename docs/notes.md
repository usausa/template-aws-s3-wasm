# 注意点

## ⚠️ 実装上の落とし穴

Blazor WASM + AWS の組み合わせで実際に踏んだ問題と対処。**流用時にも同じ制約が効く。**

### AWS SDK は browser-wasm でそのままでは動かない

2 段階の問題があり、いずれも `AWSConfigs.HttpClientFactory` の差し替え（`Template.Frontend/Application/BrowserHttpClientFactory.cs`）で解決している。

1. SDK 既定の HTTP ファクトリが `SocketsHttpHandler` を構成するが、ブラウザでは未対応で `PlatformNotSupportedException` になる。すべての通信は fetch を通す必要があるため `HttpClientHandler` ベースで組む
2. SDK のアンマーシャラーがレスポンスボディを**同期読み**するが、ブラウザのレスポンスストリームは非同期読みしか対応していない（`net_http_synchronous_reads_not_supported`）。`DelegatingHandler` で事前バッファリングし `MemoryStream` として渡す

### Cognito の `/logout` は OIDC 標準ではない

RP-Initiated Logout（`end_session_endpoint`）に対応しておらず、`client_id` + `logout_uri` 方式のみ。認証ライブラリの標準ログアウトフローは使えないため、`SignOutService` で「ローカルセッションと認証情報キャッシュを破棄 → `/logout?client_id=...&logout_uri={アプリURL}` へ遷移」を自前実装している。`logout_uri` はアプリクライアントの Allowed sign-out URLs への登録が必要。

### Cognito ドメインプレフィックスの予約語

`aws` / `amazon` / `cognito` を含めると作成時に `InvalidRequest` で失敗する。リポジトリ名をそのまま使うと該当することがある。

### `RemoteAuthenticatorView` の Action バインド

`Action="Action"` と書くとリテラル文字列 `"action"` が渡り `Invalid action 'action'` で認証画面が動かない。**`Action="@Action"`** と書く必要がある。

### ID トークンの取得はライブラリ内部仕様に依存する

Identity Pool の `GetCredentialsForIdentity` に渡せるのは **ID トークン**だが、`Microsoft.AspNetCore.Components.WebAssembly.Authentication` の C# API はアクセストークンしか公開していない。そのため `OidcTokenAccessor` が、内部の oidc-client-ts が sessionStorage に保存するキー `oidc.user:{authority}:{clientId}` を直接読んでいる。**.NET のメジャーアップデート時はキー形式の互換を再確認すること。**

### Template.Backend (Lambda) Annotations は `serverless.template` を生成し、抑止できない

`[LambdaFunction]` が 1 つでもあれば無条件に生成される（AWS のジェネレーター実装で条件がそうなっており、MSBuild プロパティ等のオプトアウトは無い。手で削除してもフルビルドで再生成される）。

本構成では未使用なうえ、内容（MemorySize 512 / Timeout 30 / **オーソライザーなし**）が実デプロイと食い違うため、誤って `dotnet lambda deploy-serverless` すると**認証なしの重複関数**ができる罠になる。そのため `Template.Backend.csproj` の `RemoveGeneratedServerlessTemplate` ターゲットでビルド後に削除している（`.gitignore` にも保険として登録）。Lambda パッケージには元から混入しない。

### Template.Backend (Lambda) Annotations はメソッドを static にできない

`[LambdaStartup]` の `ConfigureServices` もハンドラーメソッドも、生成コードがインスタンス経由で呼ぶため static にできない。DI を使わない関数でも同様で、CA1822 を局所的に抑止している。フレームワークが形を強制するもので回避手段はない。

### Razor では SVG の `<text>` を直接書けない

`<text>` は Razor が制御構文のエスケープ用に予約しているため、属性付きで書くとコンパイルエラーになる。`SeriesChart` では軸ラベルのみ組み立て済みマークアップ（`MarkupString`）として出力している。

### `HttpMethod` の名前衝突（CDK）

`Amazon.CDK.AWS.Apigatewayv2` と `Amazon.CDK.AWS.Lambda` の両方が `HttpMethod` を定義するため、using エイリアスで解決している。

### AOT publish は深いパスで失敗する（Windows）

リポジトリを深い階層に置くと、`dotnet publish -c Release` が次のエラーで落ちる。

```
error : Precompiling failed for ...\obj\Release\net10.0\wasm\for-publish\aot-in\Microsoft.AspNetCore.Components.WebAssembly.Authentication.dll with exit code 1.
error : Can not open image Microsoft.AspNetCore.Components.WebAssembly.Authentication.dll
```

AOT の中間ディレクトリ（`obj/Release/net10.0/wasm/for-publish/aot-in/`）だけで 60 文字近くを消費するため、リポジトリルートが 150 文字程度あると Windows の `MAX_PATH` (260) を超える。**メッセージがパス長を示唆しない**ので原因に辿り着きにくい。`C:\src\{repo}` のような浅い場所へ置けば解消する（`dotnet build` は AOT を通らないため成功してしまい、publish 時だけ露見する）。

## 💰 ランニングコスト

デプロイしたまま置いた場合の月額。ap-northeast-1 / CloudFront は日本向け、2026-08 時点の公開価格（USD・税別）。

| サービス | デモ運用（3 ユーザー・50 PV/月） | 軽い実運用（100 MAU・1,000 PV・5GB 転送） |
|---|---|---|
| S3（アプリ + データ数 KB） | 約 $0.0007 | 約 $0.01 |
| CloudFront | $0（無料枠内） | $0（無料枠内） |
| Cognito User Pool (Essentials) | $0（無料枠内） | $0（無料枠内） |
| Cognito Identity Pool | $0（常に無料） | $0（常に無料） |
| Lambda | $0（常時無料枠内） | $0（常時無料枠内） |
| API Gateway (HTTP API) | $0.00006 | 約 $0.013 |
| CloudFormation / IAM | $0 | $0 |
| **合計** | **実質 $0（1 セント未満）** | **約 $0.02** |

- **無料枠に収まる理由**: CloudFront は月 1TB 転送 + 1,000 万リクエストが恒久無料。Cognito User Pool は Lite / Essentials とも月 10,000 MAU まで恒久無料（Plus は無料枠なし）。Identity Pool は常に無料。Lambda は月 100 万リクエスト + 40 万 GB 秒が恒久無料
- **API Gateway だけは恒久無料枠がない**（100 万コール無料はアカウント作成から 12 か月限定）。ただし HTTP API は $1.29/100 万リクエストで、REST API の $4.25 に対して約 1/3。上表は無料枠を当てにしない前提の額
- **バケットや Distribution の「存在」自体に固定費はかからない**
- API 呼び出しは CloudFront を経由するため CloudFront のリクエスト数にも計上されるが、月 1,000 万リクエストの無料枠があるので上表は変わらない
- **無料枠が全て無くなったと仮定しても**デモ運用で約 $0.09/月、軽い実運用で約 $2.2/月。大半は Cognito の MAU 課金（$0.015/MAU）で、スケール時の主なコスト要因はここ
- `PriceClass 200` は日本を含むため必須（`PriceClass 100` は北米・欧州のみで、日本からは遠いエッジに飛ぶ）
- カスタムドメインを追加すると Route 53 ホストゾーンが約 $0.50/月かかる

## 📋 既知の制約

| 項目 | 内容 |
|---|---|
| サイレントトークン更新 | Cognito ドメインがアプリと別サイトのため、サードパーティ Cookie 制限下で iframe 更新が失敗し得る。失敗時は再ログイン誘導で成立させている（60 分セッションを許容）。恒久策はカスタムドメイン |
| API のトークン種別 | オーソライザーはアクセストークンと ID トークンのどちらも通す。種別を限定したい場合は Lambda 側で `token_use` を確認する |
| Lambda のコールドスタート | マネージドランタイムのため初回呼び出しに JIT ウォームアップが乗る（実測 700ms 程度）。詰めるなら Native AOT だが、Windows から Linux 向けにビルドするには実質 Docker が要るため採用していない |
| UI の仮想化 | `ListObjectsV2` は継続トークンで全件取得するが、一覧の仮想化は行っていない。ファイル数が多い用途では要検討 |
| シードの冪等性 | `seed-user.ps1` は配置のみで既存オブジェクトを消さないため、サンプルデータの構成を変えると旧ファイルが残る |
| 現在のデプロイ状態 | dev / prod とも一度デプロイして検証したうえで削除済み。AWS 上にスタックは存在しない（初回は[セットアップ](setup.md#-セットアップ初回構築)から実施する） |
| CI/CD | GitHub Actions は未同梱 |
