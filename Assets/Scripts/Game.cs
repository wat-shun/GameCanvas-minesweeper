using System;
using GameCanvas;
using UnityEngine;
using UnityEngine.Assertions;

// TODO:旗の実装
// TODO:リファクタリング

/*
    かなりダメなマインスイーパーの盤面

    イケてないところ
    ・配列にアクセスするとき、[y,x]でアクセスするとバグの元になるので完成で(x,y)でアクセスする（本当はゲッターとセッターをつかうべき）
    ・単体で完結してない（終了判定をGame.calc内などでやってて最悪）
*/

public class Game : GameBase
{
    // 現在の状態を記録する
    enum Mode
    {
        Title,
        Play,
        Clear,
        Gameover
    }

    // 画像描画レイヤー
    enum Layer
    {
        Background,
        Object,
        Character,
        Logo,
        UI
    }

    // 全体の状態管理系
    private Mode mode = Mode.Title;

    // スコアの管理系
    private float startTime = 0;

    private float endTime = 0;

    // ゲームバランス系
    private const int BOARD_SIZE = 10;

    private const int COUNT_BOMB = 15;

    // プレイ画面での状態管理系
    private MinesweeperBoard board;

    private int faceId = 14;
    private bool faceChanged = false;

    // クリア画面での状態管理系
    private Vector2[] planePos = new Vector2[8];

    private int[] planeSpeed = new int[8];

    // よく使う画像のサイズ
    private int cellImgSize;

    private int numImgSize;

    // プレイ画面のセルのパディング
    private Vector2 paddingBoard = new Vector2(10, 25);

    // 指定した矩形をタッチしたか
    bool isTouchRect(int x, int y, int w, int h)
    {
        return gc.isTouchEnded && x <= gc.touchX && gc.touchX <= x + w && y <= gc.touchY && gc.touchY <= y + h;
    }

    // すでにホールド処理をしたか
    private bool doneHold = false;

    override public void Start()
    {
        // 全体の初期化
        gc.ChangeBGMVolume(0.5f);

        // 定数系の変数初期化
        cellImgSize = gc.GetImageWidth(10);
        numImgSize = gc.GetImageWidth(0);

        // タイトル表示
        GameTitle();
    }

    override public void Calc()
    {
        switch (mode)
        {
            case Mode.Title:
                // 開始から3.5秒経過したらゲーム開始
                if (gc.time > 3.5f) GameStart();
                break;
            case Mode.Play:
                // 背景
                gc.DrawImage(21, gc.screenWidth - 425, gc.screenHeight - 450, (int) Layer.Background);

                // 10秒ごとにキャラの顔変更
                int sec = (int) (gc.time - startTime);
                if (sec % 10 == 0)
                {
                    if (!faceChanged)
                    {
                        faceId = gc.Random(14, 19);
                        faceChanged = true;
                    }
                }
                else
                {
                    faceChanged = false;
                }

                // タッチ位置と動作からセルを特定して処理する

                // 外枠があるのに注意してセルを特定
                int x = (int) Math.Floor((gc.touchPoint.x - paddingBoard.x) / cellImgSize) - 1;
                int y = (int) Math.Floor((gc.touchPoint.y - paddingBoard.y) / cellImgSize) - 1;

                // タッチ→開ける ホールド→旗のオンオフ
                if (gc.isTap)
                {
                    // ボード内かつ未開かつ旗がないなら開ける処理
                    if (board.inBoard(x, y) && !board.isOpen(x, y) && !board.isFlag(x, y))
                    {
                        // 開けた音を鳴らす
                        gc.PlaySE(1);
                        board.open(x, y);

                        // ゲームオーバーか
                        if (board.isGameover()) GameOver();
                        // クリアか
                        if (board.isClear()) GameClear();
                    }
                }else if (gc.isHold && !doneHold)
                {
                    if (!board.isOpen(x, y) && !doneHold)
                    {
                        // 旗処理
                        gc.PlaySE(3);
                        board.turnFlag(x, y);
                        doneHold = true;
                    }
                }

                doneHold = gc.isHold;
                break;
            case Mode.Clear:
                // リトライを押したらゲームに戻る
                if (isTouchRect(centerX(29), bottomY(29) - 30, gc.GetImageWidth(29),
                    gc.GetImageHeight(29))) GameStart();
                break;
            case Mode.Gameover:
                // リトライを押したらゲームに戻る
                if (isTouchRect(centerX(29), bottomY(29) - 30, gc.GetImageWidth(29),
                    gc.GetImageHeight(29))) GameStart();
                break;
        }
    }

    override public void Draw()
    {
        int sec, k;

        switch (mode)
        {
            case Mode.Title:
                // 画面消去
                gc.ClearScreen();
                // タイトルロゴ
                gc.DrawImage(26, 0, 0, (int) Layer.Logo);
                gc.DrawImage(39, bottomX(39), centerY(39), (int) Layer.Logo);
                // 背景
                gc.DrawImage(21, gc.screenWidth - 425, gc.screenHeight - 450, (int) Layer.Background);
                // キャラ
                gc.DrawImage(20, 10, gc.screenHeight - 250, (int) Layer.Character);
                break;
            case Mode.Play:
                // 画面消去
                gc.ClearScreen();

                // 秒の表示
                sec = (int) (gc.time - startTime);
                k = (sec == 0) ? 1 : ((int) Mathf.Log10(sec) + 1);
                int rx = gc.screenWidth - numImgSize;
                gc.DrawImage(38, rx, 0, (int) Layer.UI);
                for (int i = 0; i < k; i++)
                {
                    int id = sec % 10;
                    rx -= numImgSize;
                    gc.DrawImage(id, rx, 0, (int) Layer.UI);
                    sec /= 10;
                }

                // セルに合わせて画像を作る
                for (int y = -1; y <= BOARD_SIZE; ++y)
                {
                    for (int x = -1; x <= BOARD_SIZE; ++x)
                    {
                        // 下地を描画
                        gc.DrawImage(11, cellImgSize * (x + 1) + paddingBoard.x, cellImgSize * (y + 1) + paddingBoard.y,
                            (int) Layer.UI);

                        if (board.inBoard(x, y))
                        {
                            // 状態に合わせて描画
                            if (board.isOpen(x, y))
                            {
                                // 旗は13
                                // 爆弾か数字かで分岐
                                if (board.isBomb(x, y))
                                {
                                    gc.DrawImage(12,
                                        cellImgSize * (x + 1) + paddingBoard.x,
                                        cellImgSize * (y + 1) + paddingBoard.y,
                                        (int) Layer.UI);
                                }
                                else
                                {
                                    gc.DrawImage(board.cntAroundBomb(x, y),
                                        cellImgSize * (x + 1) + paddingBoard.x,
                                        cellImgSize * (y + 1) + paddingBoard.y,
                                        (int) Layer.UI);
                                }
                            }
                            else
                            {
                                gc.DrawImage(10, cellImgSize * (x + 1) + paddingBoard.x,
                                    cellImgSize * (y + 1) + paddingBoard.y, (int) Layer.UI);

                                // 旗があれば書く
                                if (board.isFlag(x, y))
                                {
                                    gc.DrawImage(13,
                                        cellImgSize * (x + 1) + paddingBoard.x,
                                        cellImgSize * (y + 1) + paddingBoard.y,
                                        (int) Layer.UI);
                                }
                            }
                        }
                    }
                }

                // キャラ描画
                gc.DrawImage(faceId, bottomX(faceId), bottomY(faceId), (int) Layer.Character);
                break;
            case Mode.Clear:
                // 画面消去
                gc.ClearScreen();

                // ロゴ
                gc.DrawImage(27, centerX(27), 10, (int) Layer.Logo);
                // キャラ
                int cy = centerY(22) + (int) (20 * Math.Sin(2.0f * Math.PI * gc.GetMilliSecond() / 1000)) - 20;
                gc.DrawImage(22, centerX(22), cy, (int) Layer.Character);
                // 背景
                gc.DrawImage(23, centerX(23), centerY(23), (sbyte) Layer.Background);
                // リトライ
                gc.DrawImage(29, centerX(29), bottomY(29) - 30, (int) Layer.UI);

                // 秒の表示
                sec = (int) (endTime - startTime);
                k = (sec == 0) ? 1 : ((int) Mathf.Log10(sec) + 1);
                int lx = gc.screenWidth / 2 - numImgSize * (k + 1) / 2;
                for (int i = 0; i < k; i++)
                {
                    int id = sec % 10;
                    gc.DrawImage(id, lx + numImgSize * (k - i - 1), bottomY(38) - 5, (int) Layer.UI);
                    sec /= 10;
                }
                gc.DrawImage(38, lx + numImgSize * k, bottomY(38) - 5, (int) Layer.UI);

                // ランダムに飛行機を設置
                for (int i = 0; i <= 7; ++i)
                {
                    gc.DrawImage(30 + i, planePos[i].x, planePos[i].y, (int) Layer.Object);

                    // 飛行機移動
                    planePos[i].x -= planeSpeed[i];
                    // 画面外まで出たらyをランダムにして右端に移動
                    if (planePos[i].x < -gc.GetImageWidth(30 + i))
                    {
                        planePos[i].y = gc.Random(0, bottomY(30 + i));
                        planePos[i].x = gc.screenWidth + gc.GetImageWidth(30 + i);
                    }
                }
                break;
            case Mode.Gameover:
                // 画面描画処理
                gc.ClearScreen();

                // ロゴ
                gc.DrawImage(28, centerX(28), 10, (int) Layer.Logo);
                // キャラ
                gc.DrawImage(24, centerX(24), centerY(24), (int) Layer.Character);
                // 背景
                gc.DrawImage(25, centerX(25), centerY(25), (sbyte) Layer.Background);
                // リトライ
                gc.DrawImage(29, centerX(29), bottomY(29) - 30, (int) Layer.UI);
                break;
        }
    }

    // タイトル時の初期化処理
    void GameTitle()
    {
        // モード切り替え
        mode = Mode.Title;

        // BGM止める
        gc.StopBGM();

        // タイトルの効果音再生
        gc.PlaySE(0);

    }

    // ゲーム開始時の初期化処理
    void GameStart()
    {
        // ゲーム開始時の初期化処理
        // モード切り替え
        mode = Mode.Play;

        // BGM止める
        gc.StopBGM();

        // BGM再生
        gc.PlayBGM(6);

        // 時間初期化
        startTime = gc.time;

        // キャラの表情初期化
        faceId = 14;
        faceChanged = true;

        // ゲームの状態の初期化
        board = new MinesweeperBoard(BOARD_SIZE, COUNT_BOMB);

    }

    // ゲームクリア時の初期化処理
    void GameClear()
    {
        // モード切り替え
        mode = Mode.Clear;

        // BGM止める
        gc.StopBGM();

        // クリア音再生
        gc.PlayBGM(4, false);

        // 時間記録
        endTime = gc.time;

        // 飛行機の位置と速度初期化
        for (int i = 0; i <= 7; ++i)
        {
            planePos[i].Set(gc.Random(0, gc.screenWidth), gc.Random(0, bottomY(30 + i)));
            planeSpeed[i] = gc.Random(13, 18);
        }
    }

    // ゲームオーバー時の初期化処理
    void GameOver()
    {
        // モード切り替え
        mode = Mode.Gameover;

        // BGM止める
        gc.StopBGM();

        // 爆発音再生
        gc.PlaySE(2);

        // ゲームオーバー音再生
        gc.PlayBGM(5, false);

    }

    int bottomX(int id)
    {
        return gc.screenWidth - gc.GetImageWidth(id);
    }

    int bottomY(int id)
    {
        return gc.screenHeight - gc.GetImageHeight(id);
    }

    int centerX(int id)
    {
        return gc.screenWidth / 2 - gc.GetImageWidth(id) / 2;
    }

    int centerY(int id)
    {
        return gc.screenHeight / 2 - gc.GetImageHeight(id) / 2;
    }
}