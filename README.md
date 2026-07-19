# SheBang

Windows上でUnix系のShebang（`#!`）を再現するコマンドラインランチャーです。

Shebangが記述されたスクリプトをこのプログラムに関連付けると、先頭行から実行するプログラムを判定して起動します。Shebangがないファイルは、設定されたエディタで開きます。

## 動作環境

- Windows
- .NET 10

## 使い方

```text
Shebang.exe <ファイルパス> [引数...]
```

例えば、`hello.py` が次の内容の場合:

```python
#!/usr/bin/env python
print("hello")
```

次のように実行します。

```text
Shebang.exe hello.py
```

実行対象のプログラムは、環境変数 `PATH` のディレクトリから検索されます。検索時には `PATHEXT` に定義された拡張子（通常は `.EXE` など）が使用されます。

Shebangに指定された引数と、ランチャー自身に渡された引数は、実行対象のプログラムへ引き継がれます。

## 対応するShebang

通常の形式:

```text
#!/share/bin/python
```

`env` を使う形式:

```text
#!/usr/bin/env python
```

`env -S` を使う形式:

```text
#!/usr/bin/env -S python -u
```

実行プログラム名の検索時は、Shebangに書かれたディレクトリ部分を無視します。例えば `/usr/bin/python` は `python` として検索されます。

## Shebangがないファイル

Shebangがないファイルは、設定ファイルに指定されたエディタで開きます。

設定ファイルの場所:

```text
%APPDATA%\SheBang\config.json
```

初回実行時に設定ファイルが存在しない場合は、自動的に次の内容で作成されます。

```json
{
  "Editor": "notepad.exe"
}
```

エディタを変更する場合は、`Editor` に実行ファイル名またはフルパスを指定します。

## ファイルの関連付け

Windowsの「設定」または「ファイルの種類ごとに既定のアプリを選ぶ」から、対象の拡張子を `Shebang.exe` に関連付けてください。

コマンドラインから関連付ける場合の例:

```text
assoc .py=ShebangScript
ftype ShebangScript="C:\path\to\Shebang.exe" "%1" %*
```

`C:\path\to\Shebang.exe` は、実際に配置したパスへ置き換えてください。
