﻿using ClassTranscribeDatabase;
using ClassTranscribeDatabase.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ClassTranscribeServer.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class EpubController : BaseController
    {
        private readonly WakeDownloader _wakeDownloader;
        private readonly CaptionQueries _captionQueries;

        public EpubController(WakeDownloader wakeDownloader, 
            CTDbContext context, 
            CaptionQueries captionQueries,
            ILogger<EpubController> logger) : base(context, logger)
        {
            _captionQueries = captionQueries;
            _wakeDownloader = wakeDownloader;
        }


        public class EPubSceneData
        {
            public string Image { get; set; }
            public string Text { get; set; }
            public TimeSpan Start { get; set; }
            public TimeSpan End { get; set; }
        }


        [NonAction]
        public static List<EPubSceneData> GetSceneData(JArray scenes, List<Caption> captions)
        {
            var chapters = new List<EPubSceneData>();
            var nextStart = new TimeSpan(0);
            foreach (JObject scene in scenes)
            {
                var endTime = TimeSpan.Parse(scene["end"].ToString());
                var subset = captions.Where(c => c.Begin < endTime && c.Begin >= nextStart).ToList();
                StringBuilder sb = new StringBuilder();
                subset.ForEach(c => sb.Append(c.Text + " "));
                string allText = sb.ToString();
                chapters.Add(new EPubSceneData
                {
                    Image = scene["img_file"].ToString(),
                    Start = TimeSpan.Parse(scene["start"].ToString()),
                    End = TimeSpan.Parse(scene["end"].ToString()),
                    Text = allText
                });
                nextStart = endTime;
            }
            return chapters;
        }

        /// <summary>
        /// Gets captions and images for a given video
        /// </summary>
        /// 
        [HttpGet("GetEpubData")]
        public async Task<ActionResult<List<EPubSceneData>>> GetEpubData(string mediaId, string language)
        {
            var media = _context.Medias.Find(mediaId);
            EPub epub = new EPub
            {
                Language = language,
                VideoId = media.VideoId
            };
            Video video = await _context.Videos.FindAsync(epub.VideoId);

            if (video.SceneData == null)
            {
                return NotFound();
            }

            var captions = await _captionQueries.GetCaptionsAsync(epub.VideoId, epub.Language);

            return GetSceneData(video.SceneData["Scenes"] as JArray, captions);
        }

        [HttpGet("RequestEpubCreation")]
        public ActionResult RequestEpubCreation(string mediaId)
        {
            _wakeDownloader.GenerateScenes(mediaId);
            return Ok();
        }

        // GET: api/EPubs
        [HttpGet]
        public async Task<ActionResult<IEnumerable<EPub>>> GetEPubs()
        {
            return await _context.EPubs.AsNoTracking().ToListAsync();
        }

        // GET: api/EPubs/5
        [HttpGet("{id}")]
        public async Task<ActionResult<EPub>> GetEPub(string id)
        {
            var ePub = await _context.EPubs.FindAsync(id);

            if (ePub == null)
            {
                return NotFound();
            }

            return ePub;
        }

        // PUT: api/EPubs/5
        // To protect from overposting attacks, please enable the specific properties you want to bind to, for
        // more details see https://aka.ms/RazorPagesCRUD.
        [HttpPut("{id}")]
        public async Task<IActionResult> PutEPub(string id, EPub ePub)
        {
            if (id != ePub.Id)
            {
                return BadRequest();
            }

            _context.Entry(ePub).State = EntityState.Modified;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!EPubExists(id))
                {
                    return NotFound();
                }
                else
                {
                    throw;
                }
            }

            return NoContent();
        }

        // POST: api/EPubs
        // To protect from overposting attacks, please enable the specific properties you want to bind to, for
        // more details see https://aka.ms/RazorPagesCRUD.
        [HttpPost]
        public async Task<ActionResult<EPub>> PostEPub(EPub ePub)
        {
            _context.EPubs.Add(ePub);
            await _context.SaveChangesAsync();

            return CreatedAtAction("GetEPub", new { id = ePub.Id }, ePub);
        }

        // DELETE: api/EPubs/5
        [HttpDelete("{id}")]
        public async Task<ActionResult<EPub>> DeleteEPub(string id)
        {
            var ePub = await _context.EPubs.FindAsync(id);
            if (ePub == null)
            {
                return NotFound();
            }

            _context.EPubs.Remove(ePub);
            await _context.SaveChangesAsync();

            return ePub;
        }

        private bool EPubExists(string id)
        {
            return _context.EPubs.AsNoTracking().Any(e => e.Id == id);
        }
    }
}