﻿//using Donut;
//using Donut.Structs;
using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;

namespace TeamServer.Utilities
{
    public static class Shellcode
    {
        public static byte[] AssemToShellcode(string filePath, string arguments)
        {
            char allPlatformPathSeperator = Path.DirectorySeparatorChar;
            // find the Engineer cs file and load it to a string so we can update it and then run the compiler function on it
            string path = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            //split path at bin keyword
            string[] pathSplit = path.Split("bin"); //[0] is the parent folder [1] is the bin folder
            //update each string in the array to replace \\ with the correct path seperator
            pathSplit[0] = pathSplit[0].Replace("\\", allPlatformPathSeperator.ToString());
            string tempFolder = pathSplit[0] + "temp";
            
            // remove the leading space and add " to front and end of arguments
            arguments = arguments.TrimStart(' ');
            filePath = filePath.TrimStart(' ');
          

            Process donut = new Process();
            donut.StartInfo.FileName = $"{pathSplit[0]}{allPlatformPathSeperator}Programs{allPlatformPathSeperator}Builtin{allPlatformPathSeperator}Donut_Windows{allPlatformPathSeperator}donut.exe";
            donut.StartInfo.Arguments = $"-x 2 -a 3 -b 3 -o {tempFolder}{allPlatformPathSeperator}payload.bin -p {arguments} {filePath}";
            donut.StartInfo.UseShellExecute = false;
            donut.StartInfo.RedirectStandardOutput = true;
            donut.StartInfo.RedirectStandardError = true;
            
            //redirect output to console
            donut.OutputDataReceived += (sender, args) => Console.WriteLine(args.Data);
            donut.ErrorDataReceived += (sender, args) => Console.WriteLine(args.Data);
            
            donut.Start();
            donut.WaitForExit();
             string output = donut.StandardOutput.ReadToEnd();
             string error = donut.StandardError.ReadToEnd();
             Console.WriteLine(output);
             Console.WriteLine(error);
            byte[] shellcode = File.ReadAllBytes($"{tempFolder}{allPlatformPathSeperator}payload.bin");
            return shellcode;
            
        }


        public static byte[] EncodeShellcode(byte[] shellcode)
        {
            char allPlatformPathSeperator = Path.DirectorySeparatorChar;
            string path = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            //split path at bin keyword
            string[] pathSplit = path.Split("bin"); //[0] is the parent folder [1] is the bin folder
            //update each string in the array to replace \\ with the correct path seperator
            pathSplit[0] = pathSplit[0].Replace("\\", allPlatformPathSeperator.ToString());
            string programFolder = pathSplit[0] + $"Programs{allPlatformPathSeperator}Extensions{allPlatformPathSeperator}";
            
            //spawn a process starting sng.exe 
            Process sng = new Process();
            sng.StartInfo.FileName = $"{programFolder}sng.exe";
            sng.StartInfo.UseShellExecute = false;
            sng.StartInfo.RedirectStandardOutput = true;
            sng.StartInfo.RedirectStandardError = true;
            sng.StartInfo.RedirectStandardInput = true;
            sng.Start();
            //write the shellcode to the stdin of sng.exe
            sng.StandardInput.BaseStream.Write(shellcode, 0, shellcode.Length);
            sng.StandardInput.Close();
            //read the output from sng.exe
            byte[] encodedShellcode = new byte[shellcode.Length];
            sng.StandardOutput.BaseStream.Read(encodedShellcode, 0, shellcode.Length);
            sng.WaitForExit();
            return encodedShellcode;
        }
    }
}
