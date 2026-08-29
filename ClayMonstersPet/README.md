# ClayMonstersPet

ClayMonsters用の軽量デスクトップペット表示プロセスです。

## 挙動

- 左ドラッグで個別に移動
- ランダムな移動と待機を繰り返す
- まれに睡眠し`zzz`を表示
- 複数体では列を作って移動したり、ぶつかり火花を出したりする

## Steamプレイ時間

Steamから遊ぶ場合は兄弟プロジェクト `ClayMonstersLauncher` を起動exeにしてください。  
本ペットまたは本編が動いているあいだランチャーが生存し、プレイ時間が継続します。

## ビルド

Release publish は `.NET 9 Desktop Runtime` 同梱の self-contained 単一exeになります。プレイヤーPCへの .NET インストールは不要です。

Unity開発用（StreamingAssetsへ配置）:

```powershell
dotnet publish .\ClayMonstersPet\ClayMonstersPet.csproj -c Release -o .\Assets\StreamingAssets\ClayMonstersPet
```

Steam出荷用（ゲームルート配下）:

```powershell
dotnet publish .\ClayMonstersPet\ClayMonstersPet.csproj -c Release -o .\Build\Steam\ClayMonstersPet
```

出力された `ClayMonstersPet.exe` がビルド済みゲームから見つかり次第、Unity本体は焼き出し後に終了し、このexeへ引き継ぎます。
見つからない場合は同一プロセス内の軽量ペット表示へフォールバックします。
