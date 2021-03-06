﻿using Codeplex.Data;
using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Text;
using System.Web.UI;

namespace jobrunweb
{
    public partial class Default : Page
    {
        dynamic _json = null;
        string _path = null;
        Encoding _enc = Encoding.GetEncoding("Shift_JIS");

        protected void Page_Load(object sender, EventArgs e)
        {
            try
            {
                // jsonファイル読み込み
                StreamReader reader = new StreamReader(Server.MapPath("./jobrunweb.json"), _enc);
                string jsonValue = reader.ReadToEnd();
                reader.Close();
                // DynamicJson
                _json = DynamicJson.Parse(jsonValue);
                _path = _json.batPath;

                // 認証キー取得
                string key = Page.Request.QueryString.Get("key");

                // 確認 => 不一致なら終了
                if (string.IsNullOrEmpty(key) == true) throw new Exception("unmatch:getkey");
                if (key != _json.getKey) throw new Exception("unmatch:json_getkey");

                // 実行ジョブ名取得
                string bat = Page.Request.QueryString.Get("bat");

                // ジョブ名がなければ終了
                if (string.IsNullOrEmpty(bat) == true) throw new Exception("unmatch:getbat");

                if (bat == "start" || bat == "stop")
                {
                    // UDP送信
                    runUdp(bat, "0");
                }
                else
                {
                    // ProcessにコマンドプロンプトとBATアドレスを指定して実行
                    string Argument = _json.batPath + @"Bat\" + bat + ".bat";
                    string Filename = Environment.GetEnvironmentVariable("ComSpec");
                    runProcess("/c \"" + Argument + "\"", Filename);
                }
                // IFTTT MAKERへPOST
                runPost(bat);

                // Logへ書き込み
                using (StreamWriter writer = new StreamWriter(_json.batPath + @"Log\log.txt", true, _enc))
                {
                    writer.WriteLine(DateTime.Now.ToString() + " " + bat.ToString());
                }
            }
            catch (Exception ex)
            {
                if (string.IsNullOrEmpty(_path) == true)
                {
                    Response.Write(ex.ToString());
                }
                else
                {
                    using (StreamWriter writer = new StreamWriter(_json.batPath + @"Log\log.txt", true, _enc))
                    {
                        writer.WriteLine(DateTime.Now.ToString() + ex.ToString());
                    }
                }
            }
        }

        /// <summary>
        /// CMD実行
        /// </summary>
        /// <param name="Argument">引数</param>
        /// <param name="Filename">実行ファイル</param>
        protected void runProcess(string Argument,string Filename)
        {
            Process process = new Process();
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.RedirectStandardOutput = true;
            process.StartInfo.RedirectStandardInput = false;
            process.StartInfo.CreateNoWindow = true;
            process.StartInfo.FileName = Filename;
            process.StartInfo.Arguments = Argument;
            process.Start();
        }

        /// <summary>
        /// UDP実行
        /// </summary>
        /// <param name="command">コマンド</param>
        /// <param name="no">カメラ番号</param>
        protected void runUdp(string command,string no)
        {
            //データを送信するリモートホストとポート番号
            string remoteHost = _json.remoteHost;
            int remotePort = (int)_json.remotePort;

            //UdpClientオブジェクトを作成する
            System.Net.Sockets.UdpClient udp = new System.Net.Sockets.UdpClient();

            //送信するデータを作成する
            string msg = "";
            msg = "command : " + command + "\r\n";
            msg += "camera_no : " + no + "\r\n";
            msg += "authenticate_code : \r\n";
            msg += "\r\n";
            byte[] sendBytes = Encoding.UTF8.GetBytes(msg);

            //リモートホストを指定してデータを送信する
            udp.Send(sendBytes, sendBytes.Length, remoteHost, remotePort);

            //UdpClientを閉じる
            udp.Close();
        }

        /// <summary>
        /// IFTTT MAKERへPOST
        /// </summary>
        /// <param name="bat">BATコマンド名</param>
        protected void runPost(string bat)
        {
            string makerKey = _json.makerKey;
            string makerTrigger = _json.makerTrigger;
            // IFTTT MAKER
            string url = "http://maker.ifttt.com/trigger/" + makerTrigger + "/with/key/" + makerKey;

            //POST送信する文字列を作成
            string postData = "value1=" + System.Web.HttpUtility.UrlEncode(bat, _enc);

            WebClient wc = new WebClient();
            //文字コードを指定する
            wc.Encoding = _enc;
            //ヘッダにContent-Typeを加える
            wc.Headers.Add("Content-Type", "application/x-www-form-urlencoded");
            //データを送信し、また受信する
            string resText = wc.UploadString(url, postData);
            wc.Dispose();
        }
    }
}