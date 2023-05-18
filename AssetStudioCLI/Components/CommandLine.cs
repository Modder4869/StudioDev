﻿using System;
using System.IO;
using System.Linq;
using System.CommandLine;
using System.CommandLine.Binding;
using System.CommandLine.Parsing;
using System.Text.RegularExpressions;
using AssetStudio;

namespace AssetStudioCLI
{
    public static class CommandLine
    {
        public static void Init(string[] args)
        {
            var rootCommand = RegisterOptions();
            rootCommand.Invoke(args);
        }
        public static RootCommand RegisterOptions()
        {
            var optionsBinder = new OptionsBinder();
            var rootCommand = new RootCommand()
            {
                optionsBinder.Silent,
                optionsBinder.TypeFilter,
                optionsBinder.NameFilter,
                optionsBinder.ContainerFilter,
                optionsBinder.GameName,
                optionsBinder.MapOp,
                optionsBinder.MapType,
                optionsBinder.MapName,
                optionsBinder.GroupAssetsType,
                optionsBinder.Model,
                optionsBinder.Key,
                optionsBinder.AIFile,
                optionsBinder.DummyDllFolder,
                optionsBinder.Input,
                optionsBinder.Output
            };

            rootCommand.SetHandler(Program.Run, optionsBinder);

            return rootCommand;
        }
    }
    public class Options
    {
        public bool Silent { get; set; }
        public ClassIDType[] TypeFilter { get; set; }
        public Regex[] NameFilter { get; set; }
        public Regex[] ContainerFilter { get; set; }
        public string GameName { get; set; }
        public MapOpType MapOp { get; set; }
        public ExportListType MapType { get; set; }
        public string MapName { get; set; }
        public AssetGroupOption GroupAssetsType { get; set; }
        public bool Model { get; set; }
        public byte Key { get; set; }
        public FileInfo AIFile { get; set; }
        public DirectoryInfo DummyDllFolder { get; set; }
        public FileInfo Input { get; set; }
        public DirectoryInfo Output { get; set; }
    }

    public class OptionsBinder : BinderBase<Options>
    {
        public readonly Option<bool> Silent;
        public readonly Option<ClassIDType[]> TypeFilter;
        public readonly Option<Regex[]> NameFilter;
        public readonly Option<Regex[]> ContainerFilter;
        public readonly Option<string> GameName;
        public readonly Option<MapOpType> MapOp;
        public readonly Option<ExportListType> MapType;
        public readonly Option<string> MapName;
        public readonly Option<AssetGroupOption> GroupAssetsType;
        public readonly Option<bool> Model;
        public readonly Option<byte> Key;
        public readonly Option<FileInfo> AIFile;
        public readonly Option<DirectoryInfo> DummyDllFolder;
        public readonly Argument<FileInfo> Input;
        public readonly Argument<DirectoryInfo> Output;


        public OptionsBinder()
        {
            Silent = new Option<bool>("--silent", "Hide log messages.");
            TypeFilter = new Option<ClassIDType[]>("--types", "Specify unity class type(s)") { AllowMultipleArgumentsPerToken = true, ArgumentHelpName = "Texture2D|Sprite|etc.." };
            NameFilter = new Option<Regex[]>("--names", result => result.Tokens.Select(x => new Regex(x.Value, RegexOptions.IgnoreCase)).ToArray(), false, "Specify name regex filter(s).") { AllowMultipleArgumentsPerToken = true };
            ContainerFilter = new Option<Regex[]>("--containers", result => result.Tokens.Select(x => new Regex(x.Value, RegexOptions.IgnoreCase)).ToArray(), false, "Specify container regex filter(s).") { AllowMultipleArgumentsPerToken = true };
            GameName = new Option<string>("--game", $"Specify Game.") { IsRequired = true };
            MapOp = new Option<MapOpType>("--map_op", "Specify which map to build.");
            MapType = new Option<ExportListType>("--map_type", "AssetMap output type.");
            MapName = new Option<string>("--map_name", () => "assets_map", "Specify AssetMap file name.");
            GroupAssetsType = new Option<AssetGroupOption>("--group_assets", "Specify how exported assets should be grouped.");
            Model = new Option<bool>("--models", "Enable to export models only");
            AIFile = new Option<FileInfo>("--ai_file", "Specify asset_index json file path (to recover GI containers).").LegalFilePathsOnly();
            DummyDllFolder = new Option<DirectoryInfo>("--dummy_dlls", "Specify DummyDll path.").LegalFilePathsOnly();
            Input = new Argument<FileInfo>("input_path", "Input file/folder.").LegalFilePathsOnly();
            Output = new Argument<DirectoryInfo>("output_path", "Output folder.").LegalFilePathsOnly();

            Key = new Option<byte>("--key", result =>
            {
                var value = result.Tokens.Single().Value;
                if (value.StartsWith("0x"))
                {
                    value = value[2..];
                    return Convert.ToByte(value, 0x10);
                }
                else
                {
                    return byte.Parse(value);
                }
            }, false, "XOR key to decrypt MiHoYoBinData.");

            TypeFilter.AddValidator(FilterValidator);
            NameFilter.AddValidator(FilterValidator);
            ContainerFilter.AddValidator(FilterValidator);
            Key.AddValidator(result =>
            {
                var value = result.Tokens.Single().Value;
                try
                {
                    if (value.StartsWith("0x"))
                    {
                        value = value.Substring(2);
                        Convert.ToByte(value, 0x10);
                    }
                    else
                    {
                        byte.Parse(value);
                    }
                }
                catch (Exception e)
                {
                    result.ErrorMessage = "Invalid byte value.\n" + e.Message;
                }
            });

            GameName.FromAmong(GameManager.GetGameNames());

            GroupAssetsType.SetDefaultValue(AssetGroupOption.ByType);
            MapOp.SetDefaultValue(MapOpType.None);
            MapType.SetDefaultValue(ExportListType.XML);
        }

        public void FilterValidator(OptionResult result)
        {
            var values = result.Tokens.Select(x => x.Value).ToArray();
            foreach (var val in values)
            {
                if (string.IsNullOrWhiteSpace(val))
                {
                    result.ErrorMessage = "Empty string.";
                    return;
                }

                try
                {
                    Regex.Match("", val, RegexOptions.IgnoreCase);
                }
                catch (ArgumentException e)
                {
                    result.ErrorMessage = "Invalid Regex.\n" + e.Message;
                    return;
                }
            }
        }

        protected override Options GetBoundValue(BindingContext bindingContext) =>
        new()
        {
            Silent = bindingContext.ParseResult.GetValueForOption(Silent),
            TypeFilter = bindingContext.ParseResult.GetValueForOption(TypeFilter),
            NameFilter = bindingContext.ParseResult.GetValueForOption(NameFilter),
            ContainerFilter = bindingContext.ParseResult.GetValueForOption(ContainerFilter),
            GameName = bindingContext.ParseResult.GetValueForOption(GameName),
            MapOp = bindingContext.ParseResult.GetValueForOption(MapOp),
            MapType = bindingContext.ParseResult.GetValueForOption(MapType),
            MapName = bindingContext.ParseResult.GetValueForOption(MapName),
            GroupAssetsType = bindingContext.ParseResult.GetValueForOption(GroupAssetsType),
            Model = bindingContext.ParseResult.GetValueForOption(Model),
            Key = bindingContext.ParseResult.GetValueForOption(Key),
            AIFile = bindingContext.ParseResult.GetValueForOption(AIFile),
            DummyDllFolder = bindingContext.ParseResult.GetValueForOption(DummyDllFolder),
            Input = bindingContext.ParseResult.GetValueForArgument(Input),
            Output = bindingContext.ParseResult.GetValueForArgument(Output)
        };
    }
}
