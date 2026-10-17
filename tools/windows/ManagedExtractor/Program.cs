using System.Buffers.Binary;
using System.Security.Cryptography;
using XeniaManager.Files.Utilities;
static int U(byte[] d,int o)=>(int)BinaryPrimitives.ReadUInt32BigEndian(d.AsSpan(o,4));
static byte[] Dec(byte[] key,byte[] data) { using var aes=Aes.Create();aes.Key=key;aes.IV=new byte[16];aes.Padding=PaddingMode.None;return aes.DecryptCbc(data,new byte[16],PaddingMode.None); }
Directory.CreateDirectory(args[1]);
foreach(var file in Directory.EnumerateFiles(args[0],"*.xex",SearchOption.AllDirectories)){
 try{
 byte[] d=File.ReadAllBytes(file);int header=U(d,8),sec=U(d,16),fmt=0;
 for(int i=0;i<U(d,20);i++) if(U(d,24+8*i)==0x3ff)fmt=U(d,28+8*i);
 int size=U(d,fmt),enc=BinaryPrimitives.ReadUInt16BigEndian(d.AsSpan(fmt+4)),comp=BinaryPrimitives.ReadUInt16BigEndian(d.AsSpan(fmt+6));
 var raw=d[header..];if(enc==1)raw=Dec(Dec(Convert.FromHexString("20B185A59D28FDC340583FBB0896BF91"),d[(sec+0x150)..(sec+0x160)]),raw);
 byte[] image;
 if(comp==1){using var output=new MemoryStream();int pos=0;for(int p=fmt+8;p<fmt+size;p+=8){int n=U(d,p),z=U(d,p+4);output.Write(raw,pos,n);output.Write(new byte[z]);pos+=n;}image=output.ToArray();}
 else if(comp==2){int window=System.Numerics.BitOperations.Log2((uint)U(d,fmt+8)),block=U(d,fmt+12),pos=0;using var bits=new MemoryStream();while(block!=0){int end=pos+block,next=U(raw,pos);pos+=24;while(pos+2<=end){int n=BinaryPrimitives.ReadUInt16BigEndian(raw.AsSpan(pos));pos+=2;if(n==0)break;bits.Write(raw,pos,n);pos+=n;}pos=end;block=next;}image=new LzxDecoder(window).Decompress(bits.ToArray(),U(d,sec+4));}
 else if(comp==0)image=raw;else throw new Exception("compression "+comp);
 for(int b=0;b<image.Length-256;b++){
 if(image[b]!=77||image[b+1]!=90)continue;int pe=b+BitConverter.ToInt32(image,b+60);
 if(pe<0||pe+248>image.Length||BitConverter.ToInt32(image,pe)!=0x4550)continue;
 if(BitConverter.ToUInt16(image,pe+4)!=0x14c)continue;
 var output=Path.Combine(args[1],Path.GetFileNameWithoutExtension(file));File.WriteAllBytes(output,image[b..]);Console.WriteLine(output+" "+(image.Length-b));break;
 }
 }catch(Exception ex){Console.WriteLine(Path.GetFileName(file)+": "+ex.Message);}
}
