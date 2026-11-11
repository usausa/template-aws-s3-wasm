# 構築と開発

## 🔧 テンプレートから新規プロジェクトを作る

このリポジトリを雛形として使う場合、まず名前を置き換える。置き換える対象は 3 系統ある。

| 系統 | 現在の値 | 使われる場所 |
|---|---|---|
| プロジェクト名（PascalCase） | `Template` | 名前空間・プロジェクト名・ディレクトリ名・**アセンブリ名** |
| デプロイ名（kebab-case） | `template-aws-s3-wasm` | CloudFormation スタック名 |
| Cognito ドメイン接頭辞 | `template-s3-wasm` | Managed Login の URL |

デプロイ名と Cognito ドメイン接頭辞が別値なのは、後者に予約語 `aws` を含められないため。ドメイン接頭辞は**全 AWS アカウント間で一意**である必要がある。

### 手順

リポジトリルートで実行する。先頭 3 行だけ自分の値に変える。

```powershell
$Name   = 'Acme'          # PascalCase。名前空間・アセンブリ名になる
$Deploy = 'acme-portal'   # kebab-case。CloudFormation スタック名になる
$Domain = 'acme-portal'   # Cognito ドメイン接頭辞。aws/amazon/cognito を含められない

# 1. ファイル内容を置換（.git と成果物ディレクトリは除外）
Get-ChildItem -Recurse -File |
    Where-Object { $_.FullName -notmatch '\\(\.git|bin|obj|publish-api|publish-app|cdk\.out|node_modules)\\' } |
    ForEach-Object {
        $body = Get-Content $_.FullName -Raw
        $new = $body.Replace('Template.', "$Name.").Replace('template-aws-s3-wasm', $Deploy).Replace('template-s3-wasm', $Domain)
        if ($new -ne $body) { Set-Content $_.FullName $new -NoNewline }
    }

# 2. プロジェクトファイルとディレクトリをリネーム
foreach ($p in 'Backend', 'Frontend', 'IaC') {
    Rename-Item "Template.$p/Template.$p.csproj" "$Name.$p.csproj"
    Rename-Item "Template.$p" "$Name.$p"
}
Get-ChildItem -Filter 'Template.sln*' | ForEach-Object { Rename-Item $_.FullName ($_.Name -replace '^Template', $Name) }

# 3. 確認
dotnet build
```

> ⚠️ 置換は必ず**ドット付きの `Template.`** で行う。`Template` 単体で置換すると、本文中の「テンプレート」の説明や無関係な識別子まで巻き込む。

置換後、この節はもう不要なので README から削除してよい。

なお、次の 2 つは**識別子ではなく画面に出る文言**なので上の置換では変わらない。プロジェクトに合わせて手で書き換える。

| ファイル | 文言 |
|---|---|
| `{Name}.Frontend/Components/Layout/MainLayout.razor` | ヘッダーの `S3 + Cognito WASM Template` |
| `{Name}.Frontend/Components/Pages/Home.razor` | 説明文の `Template application that ...` |

### 置換漏れで最も事故る箇所

`{Name}.IaC/ApiConstruct.cs` の Lambda ハンドラー名は、**間違っていてもデプロイが成功してしまう**。

```csharp
Handler = $"Template.Backend::Template.Backend.Functions.{functionClass}_Handle_Generated::Handle",
```

CloudFormation はこの文字列を検証しないため、アセンブリ名がずれていると `cdk deploy` は正常終了し、**API 呼び出しだけが実行時に 500**（`Could not find the specified handler assembly`）になる。上の手順どおり置換すれば直るが、手作業でリネームした場合はここを最初に疑う。

同様に、次の 2 つがずれるとデプロイは通るのにスクリプトが動かなくなる。

- `{Name}.IaC/Program.cs` のスタック ID と `scripts/common.ps1` の `$stack` — ずれると cdk outputs を読めない
- `{Name}.IaC/cdk.json` の `domainPrefix` — 他アカウントで使用中の名前だと `InvalidRequest` で失敗する

### リネーム後の確認

そのまま次の[セットアップ](#-セットアップ初回構築)を実施する。**「API」ページの 2 つの呼び出しが 200 になること**を必ず確認する。ハンドラー名の置換漏れはここでしか露見しない。

## 🚀 セットアップ（初回構築）

### 必要なもの

- .NET SDK 10.0+
- Node.js 20+（CDK CLI 実行用）
- AWS CLI v2（認証情報設定済み）

### 事前設定

`Template.IaC/cdk.json` の `context.dev.domainPrefix` を**一意な値**へ変更する。Cognito Managed Login のドメイン名になるため、全 AWS アカウント間で一意である必要がある。

> ⚠️ 小文字英数字とハイフンのみ。予約語 `aws` / `amazon` / `cognito` を**含められない**（含めるとデプロイ時に `InvalidRequest` で失敗する）。リポジトリ名をそのまま使うと引っかかることがある。

### 構築手順

```powershell
# 1. Lambda の成果物を用意（CDK がアセットとして取り込む）
./scripts/deploy-api.ps1

# 2. インフラをデプロイ
cd Template.IaC
npx --yes aws-cdk@latest bootstrap        # アカウント×リージョンで初回のみ
npx --yes aws-cdk@latest deploy -c env=dev --outputs-file ../cdk-outputs.dev.json
cd ..

# 3. ローカル開発用の設定へ反映
./scripts/update-appsettings.ps1 -Env dev

# 4. テストユーザー + サンプルデータ投入
./scripts/seed-user.ps1 -Env dev -Email user1@example.com

# 5. アプリをビルドして配信
./scripts/deploy-app.ps1 -Env dev
```

完了時に表示される URL へアクセスし、`seed-user.ps1` が出力した Email / Password でログインする。

### 動作確認の観点

- 未ログインで `/files` へアクセス → ログインへリダイレクトされる
- ログイン後、自分のデータファイルの一覧・集計値・グラフ・明細テーブルが表示される
- 一覧の「open raw」（S3 直リンク）は、**ログイン中でも**ブラウザで開くと `AccessDenied` になる
- ユーザーを 2 人作成し、他ユーザーのキーを指定した取得が AccessDenied になる
- 「API」ページの 2 つの呼び出しが成功し、返る `sub` が自分のものになる
- `/hello` を連続で叩くと `invocation` が増える（同じ実行環境が再利用されている）
- **トークンなしで API を叩くと 401 になり、Lambda が起動しない**
  （`curl https://{ApiEndpoint}/hello` → 401。CloudWatch Logs に呼び出し記録が増えないこと）
- ディープリンクの直接アクセス・リロードが動作する（SPA フォールバック）

## 🔄 更新時のデプロイ

変更した箇所によって実行するものが変わる。

| 変更した箇所 | 実行するもの |
|---|---|
| **Template.Frontend のみ** | `./scripts/deploy-app.ps1 -Env dev` |
| **Template.Backend（Lambda）** | `./scripts/deploy-api.ps1` → `cd Template.IaC; npx --yes aws-cdk@latest deploy -c env=dev --outputs-file ../cdk-outputs.dev.json` |
| **Template.IaC** | 同上（Template.Backend を変えていなければ `deploy-api.ps1` は不要） |
| **Template.IaC を変えて出力値が変わった** | 上記の後に `./scripts/update-appsettings.ps1 -Env dev` と `deploy-app.ps1` |

`deploy-app.ps1` は publish 前に `appsettings.Production.json` を cdk outputs から自動生成する（配信された WASM アプリは常に Production 環境として動作するため）。生成物は gitignore 対象。

### prod 環境

`-c env=prod` でデプロイする。dev との違いは次のとおり。

- バケットと User Pool が **RETAIN + 削除保護**になる（誤削除でデータが消えない）
- localhost のコールバック URL / CORS 許可が**付かない**
- ログ保持期間が 1 か月になる

## 🌏 デプロイ先を変更する

リージョン・アカウント・スタック名を変える場合に触る箇所。**リージョンとスタック名はコードに定数として散らばっているため、下表を漏れなく直すこと**（一箇所でも漏れるとデプロイは通るのに実行時に失敗する）。

### リージョンを変える

| ファイル | 箇所 |
|---|---|
| `Template.IaC/EnvironmentConfig.cs` | `Region` 定数 |
| `scripts/common.ps1` | `$Script:Region` |
| `Template.Frontend/wwwroot/index.html` | `preconnect` の 2 行（Cognito のホスト名にリージョンが入る） |
| `Template.Frontend/wwwroot/appsettings.json` | プレースホルダーのリージョン表記（実値はデプロイ時に自動生成されるので、体裁のみ） |

CSP と CORS のリージョンは `EnvironmentConfig.Region` から組み立てられるため、追加の変更は不要。

その後、新リージョンで `npx --yes aws-cdk@latest bootstrap` を実行してからデプロイする。

### AWS アカウントを変える

1. AWS CLI の認証情報を切り替える（プロファイル指定なら `$env:AWS_PROFILE`）
2. 新アカウント × リージョンで `bootstrap` を実行
3. `Template.IaC/cdk.json` の `domainPrefix` を変更する（**Cognito のドメインは全 AWS アカウント間で一意**のため、他アカウントで使用中の名前は取れない）

### スタック名を変える

| ファイル | 箇所 |
|---|---|
| `Template.IaC/Program.cs` | スタック ID（`template-aws-s3-wasm-{envName}`） |
| `scripts/common.ps1` | `$stack` の組み立て（cdk outputs の JSON をこの名前で引く） |

両者がずれると、デプロイは成功するのにスクリプトが outputs を読めなくなる。

### ローカル開発ポートを変える

`Template.Frontend/Properties/launchSettings.json` と `Template.IaC/EnvironmentConfig.cs` の `LocalhostOrigin` を**両方**変更する。後者は dev の Cognito コールバック URL と S3 の CORS 許可オリジンになるため、ずれるとローカルからのログインとデータ取得が失敗する。

### 環境（dev / prod）を増やす

`Template.IaC/cdk.json` の `context` に新しいキーを足し、`domainPrefix` と `allowLocalhost` を定義する。スクリプトの `-Env` は `dev` / `prod` に制限されているため、増やす場合は各スクリプトの `ValidateSet` も更新する。

## 🧹 削除（片付け）

```powershell
cd Template.IaC
npx --yes aws-cdk@latest destroy -c env=dev
```

**dev 環境**はバケットの自動削除（`autoDeleteObjects`）と LogGroup の削除込みで、スタック管理下のリソースは残骸なく消える。ただし `/aws/lambda/{スタック名}-CustomS3AutoDeleteObjects-*` の LogGroup だけは残る。バケットを空にする CDK 内蔵の Lambda が初回実行時に暗黙生成するもので、CloudFormation の管理外にあるため削除対象に含まれない。中身は空でコストもかからないが、気になる場合は手動で削除する。

**prod 環境**はバケット・User Pool が RETAIN + 削除保護のため、スタック削除後にそれらを手動で削除する必要がある。データ保全を優先した意図的な設計。API の LogGroup も同じく RETAIN なので残る。

```powershell
# 残骸の確認（スタック削除後）
aws s3api list-buckets --query "Buckets[?contains(Name,'template-aws-s3-wasm')].Name"
aws cognito-idp list-user-pools --max-results 60 --query "UserPools[?contains(Name,'template')].[Id,Name]"
aws logs describe-log-groups --query "logGroups[?contains(logGroupName,'template-aws-s3-wasm')].logGroupName"
```

## 📁 プログラム構成

```
template-aws-s3-wasm/
├── .editorconfig / Analyzers.ruleset / Directory.Build.props / Directory.Build.targets
├── AGENTS.md                                ← コーディング規約
├── Template.slnx                            ← ソリューション (3 プロジェクト)
│
├── Template.Backend/                                 ← Lambda (net10.0, マネージド dotnet10 ランタイム)
│   ├── Startup.cs                           ← [LambdaStartup]。DI コンテナへの登録
│   ├── Functions/
│   │   ├── HelloFunction.cs                 ← GET /hello。検証済みクレームを返す
│   │   └── EchoFunction.cs                  ← POST /echo。JSON ボディを受け取る
│   ├── Services/InvocationCounter.cs        ← ウォームスタート間で生き残る Singleton
│   ├── Application/                         ← Claims（クレーム抽出）/ Json（レスポンス生成）
│   ├── Models/                              ← HelloResponse / EchoModels
│   ├── FunctionSerializerContext.cs         ← 全関数で共有（LambdaSerializer はアセンブリ単位）
│   ├── Assembly.cs                          ← CLSCompliant + LambdaSerializer 属性
│   └── GlobalUsing.cs / GlobalSuppressions.cs
│
├── Template.Frontend/                                ← Blazor WebAssembly (net10.0)
│   ├── Program.cs                           ← DI 構成
│   ├── Assembly.cs / GlobalUsing.cs / GlobalSuppressions.cs / Log.cs
│   ├── Application/
│   │   ├── BrowserHttpClientFactory.cs      ← AWS SDK を browser-wasm で動かすための HTTP 差し替え
│   │   ├── SeriesParser.cs                  ← CSV → DataSeries のパース
│   │   └── ViewHelper.cs                    ← 表示ヘルパー（@using static でマークアップから使用）
│   ├── Auth/
│   │   ├── OidcTokenAccessor.cs             ← sessionStorage から ID トークン読み出し
│   │   ├── AwsCredentialsProvider.cs        ← ID トークン→一時認証情報の取得とキャッシュ
│   │   └── SignOutService.cs                ← Cognito /logout への自前サインアウト
│   ├── Components/
│   │   ├── App.razor / _Imports.razor / RedirectToLogin.cs
│   │   ├── Chart/SeriesChart.razor(.cs)     ← インライン SVG グラフ
│   │   ├── Layout/MainLayout.razor(.cs)     ← ログイン状態表示 + ログイン/ログアウト
│   │   └── Pages/
│   │       ├── Home.razor                   ← 認証状態とクレーム表示
│   │       ├── Files.razor(.cs)             ← [Authorize] 一覧・集計・グラフ・明細・直リンク
│   │       ├── Api.razor(.cs)               ← [Authorize] 認証付き API の呼び出しと結果表示
│   │       ├── Authentication.razor(.cs)    ← RemoteAuthenticatorView ({action})
│   │       └── NotFound.razor
│   ├── Helpers/MediaHelper.cs               ← 描画可否のメディア判定
│   ├── Models/                              ← UserFile / SeriesPoint / DataSeries / HelloResponse
│   ├── Services/
│   │   ├── UserFileRepository.cs            ← 自分のプレフィックスの List / Get、直リンク生成
│   │   └── ApiClient.cs                     ← 認証付き API の呼び出し
│   ├── Settings/AppSetting.cs               ← appsettings.json の App セクション
│   └── wwwroot/                             ← index.html（スプラッシュ付き）/ appsettings*.json / css
│
├── Template.IaC/                                     ← AWS CDK (C#)。Constructs パッケージとの名前空間衝突を避けフラット構成
│   ├── cdk.json                             ← 環境別 context（domainPrefix / allowLocalhost）
│   ├── Program.cs                           ← App エントリ（-c env=dev|prod）
│   ├── EnvironmentConfig.cs                 ← context の読み取りとリージョン等の定数
│   ├── Infrastructure.cs                    ← スタック本体（CA1711 対応で 'Stack' サフィックスを避けた命名）
│   ├── HostingConstruct.cs                  ← アプリバケット + CloudFront
│   ├── AuthConstruct.cs                     ← User Pool + Client + Domain + Identity Pool + ロール
│   ├── DataConstruct.cs                     ← データバケット + CORS
│   └── ApiConstruct.cs                      ← Lambda + HTTP API + JWT オーソライザー
│
├── scripts/                                 ← 下記「scripts 一覧」参照
└── README.md                                ← 本書
```

### 主要コンポーネント

**Template.Frontend / Program.cs**

- `AWSConfigs.HttpClientFactory` の差し替えを**他のどの AWS 呼び出しよりも前**に行う
- `AddOidcAuthentication` の設定は appsettings から。`email` スコープのみコード側で追加する（設定ファイルに書くと既定スコープと重複する）
- 起動時のデータ読み込みは行わない（認証前は資格情報が無いため、ページ表示時に取得する）

**Template.Frontend / AwsCredentialsProvider**

- `IAccessTokenProvider.RequestAccessToken()` でセッションの有効性を確保した上で、ID トークンを `OidcTokenAccessor` 経由で取得
- 匿名クライアントで `GetId` → `GetCredentialsForIdentity`。IdentityId はセッションにキャッシュし、ページ読み込みごとの `GetId` を省く
- 認証情報は失効 5 分前まで再利用。取得できない場合は例外ではなく `null` を返し、呼び出し側が再ログインへ誘導する

**Template.Frontend / UserFileRepository**

- `ListAsync(sub)` は継続トークンで全件取得。`ObjectUrl(key)` は画面に出す直リンク（セグメント単位で URL エスケープ）
- `AmazonS3Client` は認証情報が更新されるまで使い回す
- sub はクレームから取得してキー組み立てに使うが、**セキュリティ境界は IAM 側**。クライアントでキーを細工しても他ユーザーのデータには到達できない

**Template.IaC の構築順**

構築順は参照の連鎖で決まり、**Api（本体のみ）→ Hosting → Data → Auth → Api（ルート追加）** となる。CloudFront の `/api/*` オリジンに API のホスト名が要り、App Client の callback URL とデータバケットの CORS に CloudFront ドメインが要り、JWT オーソライザーに App Client が要る、という依存関係のため。`ApiConstruct` を「本体だけ先に作り、あとから `AddRoutes` でルートとオーソライザーを足す」2 段構えにしているのは、この連鎖を循環させないため（`AWS::ApiGatewayV2::Api` 自体は何にも依存しない）。Identity Pool は認可の仕組みが見えるよう L1 (`Cfn*`) で構築している。

### 設定ファイル（wwwroot/appsettings.json）

```json
{
  "Oidc": {
    "Authority": "https://cognito-idp.ap-northeast-1.amazonaws.com/{userPoolId}",
    "ClientId": "{userPoolClientId}",
    "ResponseType": "code"
  },
  "App": {
    "Region": "ap-northeast-1",
    "UserPoolId": "ap-northeast-1_XXXXXXXX",
    "IdentityPoolId": "ap-northeast-1:xxxxxxxx-...",
    "CognitoDomain": "https://{prefix}.auth.ap-northeast-1.amazoncognito.com",
    "DataBucket": "{data-bucket-name}",
    "ApiEndpoint": "https://{distribution}.cloudfront.net/api"
  }
}
```

> ここに載る値は**すべて公開可能**。パブリッククライアント前提で、認可は IAM と API Gateway が担保する。バケット名の推測に対してもパブリックアクセスブロックで防御している。

## 📜 scripts 一覧

すべて PowerShell。リポジトリルートから実行する。

| スクリプト | 用途 | 主な引数 |
|---|---|---|
| `deploy-api.ps1` | Template.Backend を `publish-api/` へ publish する。CDK がアセットとして取り込むため、**`cdk deploy` の前**に実行する | なし |
| `update-appsettings.ps1` | cdk outputs から `appsettings.Development.json` を生成する（ローカル開発用） | `-Env dev\|prod` |
| `seed-user.ps1` | テストユーザーを作成し、`users/{sub}/` へサンプルデータを配置する。実ユーザーの発行手順もこれに準じる | `-Email` (必須) / `-Env` / `-Password` |
| `deploy-app.ps1` | Template.Frontend を publish して S3 へ同期し、CloudFront を無効化する。`appsettings.Production.json` の生成も行う | `-Env dev\|prod` |
| `common.ps1` | 上記から dot-source される共通処理（cdk outputs の読み取り、appsettings 生成）。**直接実行しない** | — |

いずれも `cdk-outputs.{env}.json` をリポジトリルートから読む。`cdk deploy` 時に `--outputs-file` を付け忘れると動かない。

## 🧰 手動での操作手順

上記スクリプトが実行しているコマンドそのもの。CI へ組み込む・別マシンで作業する・スクリプトが途中で失敗した箇所だけやり直す、といった場合に参照する。

### 共通: スタック出力の取得

以降の手順はすべてスタックの出力値を使う。`--outputs-file` を使っていない場合は CloudFormation から直接引ける。

```powershell
$EnvName = 'dev'
$Stack   = "template-aws-s3-wasm-$EnvName"
$o = @{}
(aws cloudformation describe-stacks --stack-name $Stack --query 'Stacks[0].Outputs' | ConvertFrom-Json) |
    ForEach-Object { $o[$_.OutputKey] = $_.OutputValue }
$o
```

取得できるキー: `CloudFrontDomain` / `DistributionId` / `AppBucketName` / `DataBucketName` / `UserPoolId` / `UserPoolClientId` / `CognitoDomain` / `IdentityPoolId` / `ApiEndpoint`

### 1. 環境構築

```powershell
# (1) Lambda 成果物を publish-api/ に用意する
#     出力先はリポジトリルート直下であること（ApiConstruct が {cwd}/../publish-api を参照する）
dotnet publish Template.Backend/Template.Backend.csproj -c Release -o publish-api

# (2) デプロイ
cd Template.IaC
npx --yes aws-cdk@latest bootstrap     # アカウント × リージョンで初回のみ
npx --yes aws-cdk@latest deploy -c env=dev
cd ..
```

続けて、アプリが読む設定ファイルを出力値から作る。**配信されたアプリは常に Production 環境として動く**ため `Template.Frontend/wwwroot/appsettings.Production.json` を作成する（ローカル開発用は同じ形の `appsettings.Development.json`）。

```json
{
  "Oidc": {
    "Authority": "https://cognito-idp.ap-northeast-1.amazonaws.com/{UserPoolId}",
    "ClientId": "{UserPoolClientId}",
    "ResponseType": "code"
  },
  "App": {
    "Region": "ap-northeast-1",
    "UserPoolId": "{UserPoolId}",
    "IdentityPoolId": "{IdentityPoolId}",
    "CognitoDomain": "{CognitoDomain}",
    "DataBucket": "{DataBucketName}",
    "ApiEndpoint": "{ApiEndpoint}"
  }
}
```

### 2. アプリケーション更新時のデプロイ

```powershell
# (1) 上記 appsettings.Production.json を最新の出力値で更新しておく

# (2) publish。出力先は毎回消す
#     dotnet publish -o はクリーンしないため、古い指紋付きアセットが残り続けて上がり続ける
Remove-Item -Recurse -Force publish -ErrorAction SilentlyContinue
dotnet publish Template.Frontend/Template.Frontend.csproj -c Release -o publish

# (3) S3 へ同期。まず全部 no-cache で上げる
#     .br / .gz を除外するのは、S3 オリジンがコンテンツネゴシエーションをしないため
#     （実際に配信されるのは CloudFront の動的圧縮）
aws s3 sync publish/wwwroot "s3://$($o.AppBucketName)" --delete `
    --exclude '*.br' --exclude '*.gz' --exclude 'appsettings.Development.json' `
    --cache-control 'no-cache'

# 除外したファイルは --delete の対象外になるので、過去のデプロイ分を明示的に消す
aws s3 rm "s3://$($o.AppBucketName)" --recursive `
    --exclude '*' --include '*.br' --include '*.gz' --include 'appsettings.Development.json'

# (4) _framework/ 配下の指紋付きアセットだけ長期キャッシュへ昇格し、同時に Content-Type を固定する
#     （.wasm を application/octet-stream と推測されると CloudFront の圧縮対象から外れる）
$immutable = 'public, max-age=31536000, immutable'
aws s3 cp "s3://$($o.AppBucketName)/_framework/" "s3://$($o.AppBucketName)/_framework/" --recursive `
    --exclude '*' --include '*.wasm' `
    --content-type 'application/wasm' --cache-control $immutable --metadata-directive REPLACE
aws s3 cp "s3://$($o.AppBucketName)/_framework/" "s3://$($o.AppBucketName)/_framework/" --recursive `
    --exclude '*' --include '*.js' --exclude 'dotnet.js' --exclude 'blazor.webassembly.js' `
    --content-type 'text/javascript' --cache-control $immutable --metadata-directive REPLACE

# (5) CloudFront は 10MB 超を圧縮しない。該当アセット（AOT の native ランタイムが約 19MB）は
#     publish が生成した .br の中身を同じキーへ上書きする
Get-ChildItem publish/wwwroot/_framework -File |
    Where-Object { $_.Length -gt 9MB -and $_.Extension -in '.wasm', '.js' } |
    ForEach-Object {
        $type = if ($_.Extension -eq '.wasm') { 'application/wasm' } else { 'text/javascript' }
        aws s3 cp "$($_.FullName).br" "s3://$($o.AppBucketName)/_framework/$($_.Name)" `
            --content-encoding br --content-type $type --cache-control $immutable
    }

# (6) キャッシュ無効化（'/*' は 1 パス扱いなので無料枠内）
aws cloudfront create-invalidation --distribution-id $o.DistributionId --paths '/*'
```

`Template.Backend` を変更した場合は、上記の前に **1. 環境構築の (1)(2)** をやり直す（Lambda コードは CDK のアセットとして配信されるため、`cdk deploy` を通さないと反映されない）。

### 3. ユーザー追加

セルフサインアップは無効なので、実ユーザーの発行もこの手順で行う。

```powershell
$Email    = 'user1@example.com'
$Password = '<User Pool のパスワードポリシーを満たす値>'

# (1) 作成。招待メールは送らずパスワードを直接設定する
aws cognito-idp admin-create-user --user-pool-id $o.UserPoolId --username $Email `
    --user-attributes "Name=email,Value=$Email" "Name=email_verified,Value=true" `
    --message-action SUPPRESS

# (2) パスワードを恒久設定（FORCE_CHANGE_PASSWORD 状態を解除する）
aws cognito-idp admin-set-user-password --user-pool-id $o.UserPoolId --username $Email `
    --password $Password --permanent

# (3) sub を取得。S3 のプレフィックスと IAM の PrincipalTag はこの値をキーにする
$sub = ((aws cognito-idp admin-get-user --user-pool-id $o.UserPoolId --username $Email |
    ConvertFrom-Json).UserAttributes | Where-Object Name -eq 'sub').Value

# (4) データを配置
aws s3 cp .\localdata\ "s3://$($o.DataBucketName)/users/$sub/" --recursive
```

> ⚠️ `--user-attributes` の `Name=...,Value=...` は**必ずクォートで囲む**。囲まないと PowerShell がカンマで分割し、`Invalid parameter` になる。

> ⚠️ プレフィックスは必ず `users/{sub}/`。email など sub 以外にすると IAM 条件の `${aws:PrincipalTag/userId}` に一致せず、本人でも `AccessDenied` になる。

**ユーザーを削除する場合**は Cognito と S3 の両方を消す（スタックからは独立しているため、片方だけ残ると孤児データになる）。

```powershell
aws cognito-idp admin-delete-user --user-pool-id $o.UserPoolId --username $Email
aws s3 rm "s3://$($o.DataBucketName)/users/$sub/" --recursive
```

### 4. 環境のクリア

```powershell
cd Template.IaC
npx --yes aws-cdk@latest destroy -c env=dev
cd ..
```

> ⚠️ `destroy` も内部で synth を行うため、**`publish-api/` が存在しないと `Cannot find asset at ...\publish-api` で失敗する**（削除だけしたいのに Lambda の成果物が要る、という直感に反する挙動）。消してしまった場合は `dotnet publish Template.Backend/Template.Backend.csproj -c Release -o publish-api` をやり直してから実行する。

**prod は RETAIN + 削除保護**のため、スタック削除の前後に手作業が要る。

```powershell
# (1) User Pool の削除保護を外す
aws cognito-idp update-user-pool --user-pool-id $o.UserPoolId --deletion-protection INACTIVE

# (2) スタックを削除（RETAIN 指定のバケット・User Pool は残る）
cd Template.IaC; npx --yes aws-cdk@latest destroy -c env=prod; cd ..

# (3) 残ったバケットを空にしてから削除する（中身があると削除できない）
foreach ($b in $o.AppBucketName, $o.DataBucketName) {
    aws s3 rm "s3://$b" --recursive
    aws s3api delete-bucket --bucket $b
}

# (4) 残った User Pool とドメイン
aws cognito-idp delete-user-pool-domain --user-pool-id $o.UserPoolId --domain '<domainPrefix>'
aws cognito-idp delete-user-pool --user-pool-id $o.UserPoolId

# (5) 残った LogGroup（RETAIN 指定のものと、CDK 内蔵 Lambda の暗黙生成分）
aws logs describe-log-groups --query "logGroups[?contains(logGroupName,'template-aws-s3-wasm')].logGroupName" --output text |
    ForEach-Object { $_ -split '\s+' } | Where-Object { $_ } |
    ForEach-Object { aws logs delete-log-group --log-group-name $_ }
```

削除後の残骸確認コマンドは[🧹 削除（片付け）](#-削除片付け)を参照。

## 💻 ローカル開発

### Template.Frontend

```powershell
dotnet run --project Template.Frontend
```

`http://localhost:5250` で **dev スタックの実 Cognito / S3 に接続**する（エミュレーターは使わない）。localhost のコールバック URL と CORS 許可は dev 環境にのみ設定されている。

### Template.Backend (Lambda)

```bash
dotnet-lambda-test-tool-10.0 --path Template.Backend
```

Web UI が起動し、イベント JSON を与えてブレークポイントデバッグができる。

> ⚠️ 再現されるのは Lambda ランタイムのみで、**API Gateway の JWT オーソライザーは再現されない**。ローカルではクレームがテストイベントに書いた値になるため、任意の `sub` でユーザー別処理を検証できる一方、**401 で遮断されること自体はローカルでは確認できない**。認証の遮断は実環境の curl で確認する。

## 🧩 拡張方法

### Template.Backend (Lambda) 関数を追加する

1. `Template.Backend/Functions/` に `[LambdaFunction]` を付けたクラスを追加
2. 新しい Request / Response 型を `FunctionSerializerContext` に追加（`LambdaSerializer` 属性がアセンブリ単位のため）
3. `Template.IaC/ApiConstruct.cs` の `AddRoute` を 1 行足す

ハンドラー名はソースジェネレーターが決める: `{Assembly}::{Namespace}.{Class}_{Method}_Generated::{Method}`

publish 出力は全関数で 1 つを共有する。そのため関数が増えると各関数のコールドスタートに他関数のコード分も乗る。数個なら誤差だが、規模が大きくなったらプロジェクト分割 + 共通処理の Core ライブラリ化を検討する。

### データを配置する

任意のシステムから `s3://{DataBucket}/users/{sub}/...` へ配置する。`sub` は User Pool のユーザー ID で、`aws cognito-idp admin-get-user` で取得できる。

`date,value,note` ヘッダーの CSV はグラフ + 集計 + 明細テーブルとして描画される。それ以外のテキストは生の内容が表示される（判定は `Template.Frontend/Application/SeriesParser.cs`）。

> ⚠️ `AdminGetUser` を呼ぶとそのユーザーが MAU としてカウントされ課金対象になる。一覧目的なら `ListUsers` を使う。

### ユーザーを発行する

セルフサインアップは無効。`aws cognito-idp admin-create-user` で管理者が発行する（`scripts/seed-user.ps1` と同じ手順）。コマンド列は[🧰 手動での操作手順 - 3. ユーザー追加](#-手動での操作手順)を参照。

### その他の拡張ポイント

| 項目 | 概要 |
|---|---|
| アップロード対応 | IAM に本人プレフィックスの `s3:PutObject` を追加し、データバケットの CORS に PUT を許可する |
| カスタムドメイン | CloudFront + ACM と Cognito カスタムドメインを同一サイト（`app.example.com` / `auth.example.com`）に置く。サイレントトークン更新のサードパーティ Cookie 問題も解消する |
| CI/CD | GitHub Actions + OIDC ロール。PR で `dotnet build` + `cdk synth`、main への push でデプロイ |
| Cognito | MFA、脅威保護（Plus プラン）、招待メールによるユーザー発行 |
| 監査 | S3 サーバーアクセスログ / CloudTrail データイベント |
