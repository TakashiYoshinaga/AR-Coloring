using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using OpenCvSharp;
using OpenCvSharp.Demo;

public class ColoringScript : MonoBehaviour {
    public MeshRenderer target; //塗り絵の対象(Cube)の描画設定
    public GameObject canvas; //UIが張り付けられたCanvas
    public RawImage viewL, viewR; //画面下方の描画領域
    UnityEngine.Rect capRect;//スクショ領域を保持
    Texture2D capTexture; //スクショ画像を保持 
    Texture2D colTexture; //画像処理結果(カラー)を保持 
    Texture2D binTexture; //画像処理結果(白黒)を保持
    //Mat:OpenCVで画像を扱うための形式
    //bgrにはカラー、binには二値化画像
    Mat bgr, bin;
    // Use this for initialization
    void Start () {
        int w = Screen.width;
        int h = Screen.height;
        //画面サイズに対するROIの位置・サイズを設定
        int sx = (int)(w * 0.2f); //X始点
        int sy = (int)(h * 0.3f); //y始点
        w = (int)(w * 0.6f); //横幅
        h = (int)(h * 0.4f); //縦幅
        //原点(0,0)から画面の縦横の長さまでをキャプチャ領域とする
        capRect = new UnityEngine.Rect(sx, sy, w, h);
        //画面サイズの空画像を作成
        capTexture = new Texture2D(w, h, TextureFormat.RGB24, false);
    }

    IEnumerator ImageProcessing()
    {
        canvas.SetActive(false);//Canvas上のUIを一時的に消す
        yield return new WaitForEndOfFrame();//フレーム終了を待つ
        CreateImage(); //画像の生成
        Point[] corners; //矩形の4頂点を格納(予定)
        FindRect(out corners); //矩形の認識
        TransformImage(corners); //正面から見た画像に変換
        ShowImage(); //画像の表示
        //Mat用に確保したメモリを解放
        bgr.Release();
        bin.Release();
        canvas.SetActive(true);//Canvas上のUIを再表示
    }
    void TransformImage(Point[] corners)
    {
        //4頂点が検出されていなければ何もしない
        if (corners == null) return;
        //4頂点の並べ替え
        SortCorners(corners);
        //検出された4頂点の座標を入力
        Point2f[] input = { corners[0], corners[1],
                         corners[2], corners[3] };
        //テクスチャとして使用する正方形画像の4頂点の座標を入力
        Point2f[] square =
                 { new Point2f(0, 0), new Point2f(0, 255),
                new Point2f(255, 255), new Point2f(255, 0) };
        //歪んだ四角形を正方形に変換するパラメータを計算
        Mat transform = Cv2.GetPerspectiveTransform(input, square);
        //変換パラメータに基づいて画像を生成
        Cv2.WarpPerspective(bgr, bgr, transform, new Size(256, 256));
        int s = (int)(256 * 0.05f); //今回、枠の太さを幅の5%と設計
        int w = (int)(256 * 0.9f); //両サイドを差し引いた90%が内側の幅
        OpenCvSharp.Rect innerRect = new OpenCvSharp.Rect(s, s, w, w);
        bgr = bgr[innerRect];

    }
    void SortCorners(Point[] corners)
    {
        System.Array.Sort(corners, (a, b) => a.X.CompareTo(b.X));
        if (corners[0].Y > corners[1].Y)
        {
            corners.Swap(0, 1);
        }
        if (corners[3].Y > corners[2].Y)
        {
            corners.Swap(2, 3);
        }
    }
    void FindRect(out Point[] corners)
    {
        //頂点をnull(空)で初期化
        corners = null;
        //輪郭を構成する点と階層
        Point[][] contours;
        HierarchyIndex[] h;
        //輪郭認識
        bin.FindContours(out contours, out h, RetrievalModes.External,
                             ContourApproximationModes.ApproxSimple);
        //面積が最大となる輪郭を処理対象とする
        double maxArea = 0;
        for (int i = 0; i < contours.Length; i++)
        {
            //輪郭の長さを算出
            double length = Cv2.ArcLength(contours[i], true);
            //多角形近似(全周の1%以内の誤差を許容)
            Point[] tmp = Cv2.ApproxPolyDP(contours[i], length * 0.01f, true);

            double area = Cv2.ContourArea(contours[i]);
            if (tmp.Length == 4 && area > maxArea)
            {
                maxArea = area;
                corners = tmp;
            }
        }
        //面積最大の輪郭cornersをbgr上に描画
        /*if (corners != null)
        {
            bgr.DrawContours(new Point[][] { corners }, 0, Scalar.Red, 5);
            //各頂点の位置に円を描画
            for (int i = 0; i < corners.Length; i++)
            {
                bgr.Circle(corners[i], 20, Scalar.Blue, 5);
            }
        }*/
    }

    void CreateImage()
    {
        capTexture.ReadPixels(capRect, 0, 0);//キャプチャ開始
        capTexture.Apply();//各画素の色をテクスチャに反映
        //Texure2DをMatに変換
        bgr = Unity.TextureToMat(capTexture);
        //カラー画像をグレースケール(濃淡)画像に変換
        bin = bgr.CvtColor(ColorConversionCodes.BGR2GRAY);
        //しきい値100で二値化。結果を白黒反転
        //bin = bin.Threshold(100, 255, ThresholdTypes.BinaryInv);
        bin = bin.Threshold(100, 255, ThresholdTypes.Otsu);
        Cv2.BitwiseNot(bin, bin);
    }
    void ShowImage()
    {
        //Textureが画像を保持しているならいったん削除
        if (colTexture != null) { DestroyImmediate(colTexture); }
        if (binTexture != null) { DestroyImmediate(binTexture); }
        //Matをテクスチャに変換
        colTexture = Unity.MatToTexture(bgr);
        binTexture = Unity.MatToTexture(bin);
        //RawImageに画像を表示
        viewL.texture = colTexture;
        viewR.texture = binTexture;
        /*下記の一行でオブジェクトにテクスチャを張り付け*/
        target.material.mainTexture = colTexture;
    }
    public void StartCV()
    {
        StartCoroutine(ImageProcessing());//コルーチンの実行
    }

    // Update is called once per frame
    void Update () {
		
	}
}
