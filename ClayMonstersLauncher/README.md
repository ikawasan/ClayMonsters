# ClayMonstersLauncher

Steamの起動exe用の極小ランチャーです。本編またはデスクトップペットが動いているあいだプロセスを維持し、Steamプレイ時間を継続します。

## 役割

1. Steam が本ランチャーを起動する
2. 同じフォルダの `ClayMonsters.exe` を起動する
3. `ClayMonsters` または `ClayMonstersPet` が生きている間は終了しない
4. 本編→ペット引き継ぎの短い隙間だけ `launcher_keepalive.txt` を使う（約45秒で失効。ペット起動時と終了時に削除）
5. 両方とも一定時間いなくなったら終了する（ペット引き継ぎ／再起動の猶予あり）

## Steam設定

ビルド成果物の配置例:

```text
<game root>/
  ClayMonstersLauncher.exe   ← Steamの起動アイテム
  ClayMonsters.exe           ← Unityプレイヤー
  ClayMonsters_Data/
  ClayMonstersPet/
    ClayMonstersPet.exe
  ...
```

Steamworks / Steamクライアント:

- 起動オプションの実行ファイル: `ClayMonstersLauncher.exe`
- 作業ディレクトリ: ゲームルート（通常は自動）

## ビルド

Release publish は `.NET 9 Desktop Runtime` 同梱の self-contained 単一exeになります。プレイヤーPCへの .NET インストールは不要です。

```powershell
dotnet publish .\ClayMonstersLauncher\ClayMonstersLauncher.csproj -c Release -o .\Build\Steam
```

UnityのWindowsビルドも同じ `Build\Steam` に出すか、ランチャーだけをプレイヤー出力フォルダへコピーしてください。

ペットexeも同フォルダへ配置する:

```powershell
dotnet publish .\ClayMonstersPet\ClayMonstersPet.csproj -c Release -o .\Build\Steam\ClayMonstersPet
```

## ローカル動作チェック（Steamなし）

1. UnityでWindowsプレイヤーをビルドし `ClayMonsters.exe` を用意
2. 上記publishでランチャーとペットを同じフォルダへ出力
3. `ClayMonstersLauncher.exe` をダブルクリック
4. タスクマネージャで `ClayMonstersLauncher` が残ることを確認
5. ゲーム内でデスクトップペットを起動し本編が閉じたあと、ランチャーが残ったままペットが表示されることを確認
6. ペットを終了すると数秒後にランチャーも終了することを確認

Editor再生や `ClayMonstersPet.exe` 直起動ではランチャーは使いません（従来どおりで問題ありません）。

## 引数

| 引数 | 意味 |
|------|------|
| `--game <path>` | 本編exeパス（省略時は隣の ClayMonsters.exe） |
| `--game-process <name>` | 本編プロセス名（既定: ClayMonsters） |
| `--pet-process <name>` | ペットプロセス名（既定: ClayMonstersPet） |
| `--grace-ms <int>` | 両方終了後の終了猶予ミリ秒（既定: 8000） |
