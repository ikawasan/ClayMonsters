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

.NET 9 Desktop Runtime が必要です。

```powershell
dotnet publish .\ClayMonstersPet\ClayMonstersPet.csproj -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -o .\Assets\StreamingAssets\ClayMonstersPet
```

出力された `ClayMonstersPet.exe` がビルド済みゲームから見つかり次第、Unity本体は焼き出し後に終了し、このexeへ引き継ぎます。
見つからない場合は同一プロセス内の軽量ペット表示へフォールバックします。
