using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace URenamer
{
	class Program
	{
		static void OutputUsage(string title)
		{
			Console.Write(
$@"{title}

Windows Explorer Drag-n-Drop Usage:
  Simply drop an asset bundle file into {AppDomain.CurrentDomain.FriendlyName} to rename it to the first container name.

Command Line Usage:
  {AppDomain.CurrentDomain.FriendlyName} [-s] <source> [-d <destination>]

  -s source               Path of asset bundle to rename.
  -d destination          Path of destination folder.

Press any key to exit . . ."
			);

			Console.ReadKey();
		}

		struct Block
		{
			public uint UncompressedSize;
			public uint CompressedSize;
			public ushort Flags;
		}

		struct DirectoryInfo
		{
			public ulong Offset;
			public ulong Size;
			public uint Flags;
			public string Path;
		}

		[STAThread]
		static void Main(string[] args)
		{
			if (args.Length == 0)
			{
				OutputUsage("Tool to rename asset bundles by Lamp");
				return;
			}
			List<string> sArg = new List<string>();
			string dArg = null;
			for (int i = 0, nexti = 1; i < args.Length; i = nexti, nexti++)
			{
				var arg = args[i];
				if (arg.Equals("-s") && args.Length > nexti)
				{
					sArg.Add(args[nexti++]);
				}
				else if (arg.Equals("-d") && args.Length > nexti)
				{
					dArg = args[nexti++];
				}
				else
				{
					sArg.Add(args[i]);
				}
			}

			if (sArg.Count == 0)
			{
				OutputUsage("Source path not specified!");
				return;
			}

			foreach (string s in sArg)
			{
				if (!File.Exists(s))
				{
					OutputUsage($"Source file '{Path.GetFullPath(s)}' does not exist!");
					return;
				}
			}

			if (string.IsNullOrEmpty(dArg))
			{
				using (FolderBrowserDialog fbd = new FolderBrowserDialog())
				{
					fbd.SelectedPath = Path.GetDirectoryName(sArg[0]);
					fbd.Description = "Select a destination folder";
					DialogResult result = fbd.ShowDialog();
					if (result == DialogResult.OK && !string.IsNullOrWhiteSpace(fbd.SelectedPath))
						dArg = fbd.SelectedPath;
					else
						return;
				}
			}

#if !DEBUG
			try
#endif
			{
				foreach (string s in sArg)
				{
					List<Block> blocks = new List<Block>();
					List<DirectoryInfo> directories = new List<DirectoryInfo>();

					using (Stream file = File.OpenRead(s))
					{
						using (FileReader reader = new FileReader(file, EndianType.BigEndian))
						{
							if (!reader.TryReadStringNullTerm(out string type) || type != "UnityFS") // Type
							{
								throw new NotSupportedException("File is not UnityFS");
							}
							if (!reader.TryReadInt32(out int _) ||                                   // Version
								!reader.TryReadStringNullTerm(out string _) ||                       // UnityWebBundleVersion
								!reader.TryReadStringNullTerm(out string _))                         // UnityWebMinimumRevision
							{
								throw new InvalidDataException("Cannot read file header");
							}
							long bundleSizePtr = file.Position;
							if (!reader.TryReadUInt64(out ulong bundleSize) ||                       // BundleSize **** Need to update too after stripping PGR header (-= 0x46)
								!reader.TryReadInt32(out int metadataSize) ||                        // MetadataSize
								metadataSize == 0 ||
								!reader.TryReadInt32(out int uncompressedMetadataSize) ||            // UncompressedMetadataSize
								uncompressedMetadataSize < 24 ||
								!reader.TryReadInt32(out int flags))                                 // Flags **** Need to update too after stripping PGR header (&= 0xFFFFFDFF)
							{
								throw new InvalidDataException("Cannot read file header");
							}
							if ((flags & 0x200) != 0)
							{
								reader.ReadBytes(0x46);
							}
							Console.WriteLine("Reading metadata...");
							if ((flags & 0x00000080) != 0)
							{
								long metaposition = (long)bundleSize - metadataSize;
								if (metaposition < 0 || metaposition > file.Length)
								{
									throw new DataMisalignedException("Metadata offset is out of bounds");
								}
								throw new NotImplementedException($"Unsupported metadata position");
								file.Position = metaposition;
							}
							if ((flags & 0x0000003e) != 2)
							{
								throw new NotImplementedException($"Compresstion type '0x{flags & 0x0000003f:X2}' is not supported");
							}

							using (MemoryStream uncompressedMetadata = new MemoryStream(new byte[uncompressedMetadataSize]))
							{
								using (Lz4DecodeStream decodeStream = new Lz4DecodeStream(file, metadataSize))
								{
									decodeStream.ReadBuffer(uncompressedMetadata, uncompressedMetadataSize);
									uncompressedMetadata.Position = 0;
									using (FileReader metaReader = new FileReader(uncompressedMetadata, EndianType.BigEndian))
									{
										if (!metaReader.TryReadHash128(out Guid _))
										{
											throw new InvalidDataException("Cannot read file metadata");
										}

										if (!metaReader.TryReadUInt32(out uint blockcount) || blockcount * 10 + 20 > uncompressedMetadataSize)
										{
											throw new InsufficientMemoryException($"Block count of {blockcount} is too large for metadata size");
										}
										string plural = (blockcount == 1) ? "" : "s";
										Console.WriteLine($"{blockcount} block{plural} found:");
										for (int i = 0; i < blockcount; i++)
										{
											metaReader.TryReadUInt32(out uint uncompressedsize);
											metaReader.TryReadUInt32(out uint compressedsize);
											metaReader.TryReadUInt16(out ushort blockflags);
											Console.WriteLine($"  {i}: ({uncompressedsize}) {compressedsize} 0x{blockflags:X4}");
											blocks.Add(new Block { UncompressedSize = uncompressedsize, CompressedSize = compressedsize, Flags = blockflags });
										}

										if (!metaReader.TryReadUInt32(out uint directorycount) || directorycount * 10 + blockcount * 10 + 20 > uncompressedMetadataSize)
										{
											throw new InsufficientMemoryException($"Directory count of {directorycount} is too large for metadata size");
										}
										plural = (directorycount == 1) ? "y" : "ies";
										Console.WriteLine($"{directorycount} director{plural} found:");
										for (int i = 0; i < directorycount; i++)
										{
											metaReader.TryReadUInt64(out ulong offset);
											metaReader.TryReadUInt64(out ulong size);
											metaReader.TryReadUInt32(out uint directoryflags);
#warning TODO: Limit string parsing to remaining array size
											metaReader.TryReadStringNullTerm(out string path);
											Console.WriteLine($"  {i}: @{offset:X} {size} {directoryflags} \"{path}\"");
											directories.Add(new DirectoryInfo { Offset = offset, Size = size, Flags = directoryflags, Path = path });
										}
									}
								}
							}
						}
					}
					var bestfile = dArg + @"\" + directories[0].Path;
					if (!File.Exists(bestfile))
					{
						File.Copy(s, bestfile);
					}
					else
					{
						for (int i = 1; ; i++)
						{
							if (!File.Exists(bestfile + $" ({i})"))
							{
								File.Copy(s, bestfile + $" ({i})");
								break;
							}
						}
					}
				}
			}
#if !DEBUG
			catch (Exception e)
			{
				Console.WriteLine(
$@"An error occurred trying to read {sArg}
{e.StackTrace}

{e.Message}"
);
				Console.ReadKey();
				return;
			}
#endif
		}
	}
}
