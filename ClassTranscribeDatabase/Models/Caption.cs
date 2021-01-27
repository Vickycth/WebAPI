﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization;
using System.Text;

namespace ClassTranscribeDatabase.Models
{
    public enum CaptionType
    {
        // Since these are persisted in the database these integer values are immutable once assigned (hence explicit)

        TextCaption = 0,
        AudioDescription = 1
    }
    /// <summary>
    /// Each line of caption is stored as a row in the database.
    /// </summary>
    public class Caption : Entity
    {
        public int Index { get; set; }
        public TimeSpan Begin { get; set; }
        public TimeSpan End { get; set; }
        public string Text { get; set; }
        public string TranscriptionId { get; set; }
        public int UpVote { get; set; }
        public int DownVote { get; set; }
        [SwaggerIgnore]
        [IgnoreDataMember]
        public virtual Transcription Transcription { get; set; }
        public CaptionType CaptionType { get; set; }

        private string GetEscapedText() {
            // <>& must be escaped &lt &gt &amp; The replacement order is important
            String escape = Text.Replace("&","&amp;").Replace("<","&lt;").Replace(">","&gt;");
            // The VTT spec says the text may not contain '-->'
            while(escape.Contains("-->")) {
                escape = escape.Replace("-->","=>");
            }
            // The string may not contain an empty line
            while(escape.Contains("\n\n")) {
                escape = escape.Replace("\n\n","\n");
            }
            return escape;
        }
        
        /// <summary>
        /// Convert a line of caption to an srt subtitle format.
        /// See https://en.wikipedia.org/wiki/SubRip
        /// </summary>
        public string SrtSubtitle(int reindex)
        {
            string time= string.Format("{0:hh\\:mm\\:ss\\,fff} --> {1:hh\\:mm\\:ss\\,fff}", Begin, End);
            return $"\n{reindex}\n{time}\n{GetEscapedText()}\n\n";
        }

        /// <summary>
        /// Convert a line of caption to an webVTT subtitle format.
        /// See https://developer.mozilla.org/en-US/docs/Web/API/WebVTT_API
        /// </summary>
        public string WebVTTSubtitle()
        {
            string time = string.Format("{0:hh\\:mm\\:ss\\.fff} --> {1:hh\\:mm\\:ss\\.fff}", Begin, End);
            return $"\n{time}\n{GetEscapedText()}\n\n";
        }

        /// <summary>
        /// Converts a long line of recognizedSpeech into smaller chunks of Globals.CAPTION_LENGTH characters,
        /// and appends to a list of captions.
        /// </summary>
        /// <param name="captions">A pre-existing list of captions.</param>
        /// <param name="Begin">The beginning time stamp of the recognizedSpeech</param>
        /// <param name="End">The end time stamp of the recognizedSpeech</param>
        /// <param name="recognizedSpeech">Recognized Speech received from the Speech Services API.</param>
        public static List<Caption> ToCaptionEntitiesInterpolate(int captionsCount, TimeSpan Begin, TimeSpan End, string recognizedSpeech)
        {
            List<Caption> captions = new List<Caption>();
            int captionLength = Globals.CAPTION_LENGTH;
            int currCounter = captionsCount + 1;
            string tempCaption = recognizedSpeech;
            string caption;
            int newDuration;
            TimeSpan curBegin = Begin;
            TimeSpan curDuration = End.Subtract(Begin);
            TimeSpan curEnd;
            while (tempCaption.Length > captionLength)
            {
                newDuration = Convert.ToInt32(captionLength * curDuration.TotalMilliseconds / tempCaption.Length);
                int index = tempCaption.IndexOf(' ', captionLength);

                if (index == -1)
                {
                    caption = tempCaption;
                    tempCaption = "";
                }
                else
                {
                    caption = tempCaption.Substring(0, index);
                    tempCaption = tempCaption.Substring(index);
                    tempCaption = tempCaption.Trim();
                }
                curEnd = curBegin.Add(new TimeSpan(0, 0, 0, 0, newDuration));
                captions.Add(new Caption
                {
                    Index = currCounter++,
                    Begin = curBegin,
                    End = curEnd,
                    Text = caption
                });
                curBegin = curEnd;
                curDuration = End.Subtract(curBegin);
            }
            if (tempCaption.Length > 0)
            {
                newDuration = Convert.ToInt32(captionLength * curDuration.TotalMilliseconds / tempCaption.Length);
                curEnd = curBegin.Add(new TimeSpan(0, 0, 0, 0, newDuration));
                captions.Add(new Caption
                {
                    Index = currCounter++,
                    Begin = curBegin,
                    End = curEnd,
                    Text = tempCaption
                });
                curBegin = curEnd;
                curDuration = End.Subtract(curBegin);
            }
            return captions;
        }

        /// <summary>
        /// Generate an srt file from a list of captions.
        /// </summary>
        /// <returns>The path of the generated srt file</returns>
        public static string GenerateSrtFile(List<Caption> captions)
        {
            string srtFile = CommonUtils.GetTmpFile();
            string header = "";
            StringBuilder content = new StringBuilder(header, 100 * captions.Count);
            int captionCounter = 1;
            foreach (Caption caption in captions)
            {
                content.Append(caption.SrtSubtitle(captionCounter));
                captionCounter ++;
            }
            WriteTextToFile(content.ToString(), srtFile);
            return srtFile;
        }

        /// <summary>
        /// Generate a webVTT file from a list of captions.
        /// </summary>
        /// <returns>The path of the generated vtt file</returns>
        public static string GenerateWebVTTFile(List<Caption> captions, string language)
        {
            string now = DateTime.UtcNow.ToString("o", System.Globalization.CultureInfo.InvariantCulture);
           
            string header1 = $"WEBVTT Kind: captions; Language: {language}\n\n";
            string header2 = $"NOTE\nCreated on {now} by ClassTranscribe\n\n";
            StringBuilder content = new StringBuilder(header1, 100 * captions.Count);
            content.Append(header2);

            foreach (Caption caption in captions)
            {
                content.Append( caption.WebVTTSubtitle());
            }
            string vttFile = CommonUtils.GetTmpFile();
            WriteTextToFile(content.ToString(), vttFile);
            return vttFile;
        }

        /// <summary>
        /// Parse a WebVTT file into a list of captions.
        /// </summary>
        /// <returns>A list of the caption representing the vtt file or null if unsuccessful</returns>
        public static List<Caption> WebVTTFileToCaption(string file)
        {
            List<Caption> captions = new List<Caption>();
            string text = File.ReadAllText(file);
            string[] cues = text.Split("\n\n");
            int idx = 0;

            for (int i = 0; i < cues.Length; i++)
            {
                var cue = cues[i];
                if (i == 0 && cue.Substring(0, 6) != "WEBVTT") return null;

                if (cue.Contains("-->"))
                {
                    string[] lines = cue.Split("\n");
                    Caption caption = new Caption
                    {
                        Text = "",
                        Index = idx
                    };
                    idx++;

                    for (int j = 0; j < lines.Length; j++)
                    {
                        var line = lines[j];
                        if (line.Contains("-->"))
                        {
                            // Try parse vtt timestamp into TimeSpan
                            string[] timestamps = line.Split("-->");
                            caption.Begin = TimeSpan.Parse(timestamps[0].Trim());
                            caption.End = TimeSpan.Parse(timestamps[1].Trim());
                        }
                        else
                        {
                            // Otherwise is cue payload
                            caption.Text += line;
                        }
                    }
                    captions.Add(caption);
                }
            }
            return captions;
        }

        /// <summary>
        /// Write text to a file.
        /// </summary>
        public static void WriteTextToFile(string text, string file)
        {
            //Pass the filepath and filename to the StreamWriter Constructor
            StreamWriter sw = new StreamWriter(file, false, Encoding.UTF8);
            //Write a line of text
            sw.WriteLine(text);
            //Close the file
            sw.Close();
        }
    }
}
