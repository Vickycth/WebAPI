﻿using System.ComponentModel.DataAnnotations.Schema;
using System.IO;
using System.Runtime.Serialization;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace ClassTranscribeDatabase.Models
{

    public class FileRecord : Entity
    {
        public FileRecord(string path)
        {
            char separator = System.IO.Path.DirectorySeparatorChar;
            this.Path = path;
            this.FileName = path.Substring(path.LastIndexOf(separator) + 1);
            this.Hash = ComputeSha256HashForFile(this.Path);
        }
        public FileRecord() { }
        public string FileName { get; set; }
        [IgnoreDataMember]
        public string PrivatePath { get; set; }
        [NotMapped]
        public string Path
        {
            get
            {
                string p = PrivatePath;
                // Windows
                if (System.IO.Path.DirectorySeparatorChar == '\\')
                {
                    p = PrivatePath.Replace('\\', '/');
                }
                p = p.Substring(p.LastIndexOf("/data/") + 6);
                return System.IO.Path.Combine(Globals.appSettings.DATA_DIRECTORY, p);
            }
            set
            {
                // Windows
                if (value.Contains('\\'))
                {
                    value = value.Replace('\\', '/');
                }
                PrivatePath = value.Substring(value.LastIndexOf("/data/"));
            }
        }
        [IgnoreDataMember]
        public string VMPath
        {
            get
            {
                return PrivatePath;
            }
        }
        [IgnoreDataMember]
        public string Hash { get; set; }

        public static string ComputeSha256HashForFile(string filePath)
        {
            using (FileStream stream = File.OpenRead(filePath))
            {
                // Create a SHA256
                SHA256 Sha256 = SHA256.Create();

                // ComputeHash - returns byte array  
                byte[] bytes = Sha256.ComputeHash(stream);
                // Convert byte array to a string   

                StringBuilder builder = new StringBuilder();
                for (int i = 0; i < bytes.Length; i++)
                {
                    builder.Append(bytes[i].ToString("x2"));
                }
                return builder.ToString();
            }
        }

        public async Task DeleteFileRecordAsync(CTDbContext context)
        {
            if (File.Exists(Path))
            {
                File.Delete(Path);
            }
            var dbFileRecord = await context.FileRecords.FindAsync(Id);
            if (dbFileRecord != null)
            {
                context.FileRecords.Remove(dbFileRecord);
                await context.SaveChangesAsync();
            }            
        }
    }
}
