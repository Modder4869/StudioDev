using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Security.Cryptography;
using System.Windows.Forms;
namespace AssetStudio.GUI
{
    public static class GameHandler
    {
        public static void HandleOPBR(Game game)
        {

            using var openFileDialog = new OpenFileDialog
            {
                Filter = "Text/TSV files (*.txt;*.tsv)|*.txt;*.tsv|All Files (*.*)|*.*",
                Title = "Select OPBR Asset Index File ab_list.txt",
                Multiselect = false
            };

            if (openFileDialog.ShowDialog() != DialogResult.OK)
            {
                Console.WriteLine("No file selected.");
                return;
            }

            string filePath = openFileDialog.FileName;

            if (!File.Exists(filePath))
            {
                Console.WriteLine("File not found: " + filePath);
                return;
            }
            var result = new Dictionary<string, (string originalName, string ifEncrypt)>();
            var lines = File.ReadAllLines(filePath);

            foreach (var line in lines)
            {
                if (string.IsNullOrWhiteSpace(line))
                    continue;
                var parts = Regex.Split(line.Trim(), @"\s+");
                if (parts.Length < 2)
                    continue;

                string first = parts.First();
                string last = parts.Last();
                string hash = $"{ComputeMD5($"{first}__OPBR__")}.unity3d";

                result[hash] = (first, last);
            }

            Console.WriteLine($"Parsed {result.Count} entries.");
            game.Data = result;
            //string key = "0018d0825ea780a184c2b3ab1a0b0d9b.unity3d";

            //if (result.TryGetValue(key, out var value))
            //{
            //    Console.WriteLine($"Found key {key}: Name={value.originalName}, Encrypt={value.ifEncrypt}");
            //}
            //else
            //{
            //    Console.WriteLine($"Key {key} not found in dictionary.");
            //}
            
        }

        // Helper: compute MD5 hash of a string
        private static string ComputeMD5(string input)
        {
            using var md5 = MD5.Create();
            byte[] bytes = md5.ComputeHash(Encoding.UTF8.GetBytes(input));
            var sb = new StringBuilder();
            foreach (var b in bytes)
                sb.Append(b.ToString("x2"));
            return sb.ToString();
        }
    }
}
