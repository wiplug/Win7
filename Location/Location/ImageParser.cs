// Copyright (c) 2012, Fuji Xerox Co., Ltd.
// All rights reserved.
// Author: Surendar Chandra, FX Palo Alto Laboratory, Inc.

using System;
using System.Collections;
using System.Text;
using System.IO;

namespace location
{
    class Attachment
    {
        Hashtable htFields = new Hashtable();
        byte[] data = null;
        string sFileName = null;

        public byte[] Data
        {
            get { return data; }
            set {
                data = value;                
            }
        }
        public bool AddField(string sField, string sValue)
        {
            if (htFields.Contains(sField) == false)
            {
                htFields.Add(sField, sValue);
                return true;
            }
            return false;
        }

        public string FileName
        {
            get
            {
                return sFileName;
            }
        }
        public string GetFileName()
        {
            if (sFileName != null && sFileName != string.Empty)
            {
                return sFileName;
            }
            if (htFields == null)
                return null;
            if (htFields.Count < 1)
                return null;
            if (htFields.Contains("Content-Type") == false)
            {
                return null;
            }
            if (htFields.Contains("Content-ID") == false)
            {
                return null;
            }
            string cntType  = ((string)htFields["Content-Type"]).Trim();
            sFileName    = ((string)htFields["Content-ID"]).Trim();
            if (cntType.StartsWith("image"))
            {
                string[] split = cntType.Split("/".ToCharArray());
                if (split.Length > 1)
                {
                    sFileName = Path.ChangeExtension(sFileName, split[1]);
                }
            }
            return sFileName;
        }

        public string ContentID
        {
            get
            {
                if (htFields == null)
                    return null;
                if (htFields.Count < 1)
                    return null;
                if (htFields.Contains("Content-ID") == false)
                {
                    return null;
                }
                else
                {
                    return ((string)htFields["Content-ID"]).Trim();
                }
            }
        }

        public string Extension
        {
            get
            {
                if (htFields == null)
                    return null;
                if (htFields.Count < 1)
                    return null;
                if (htFields.Contains("Content-Type") == false)
                {
                    return null;
                }
                else
                {
                    string cntType = ((string)htFields["Content-Type"]).Trim();
                    string[] split = cntType.Split("/".ToCharArray());
                    if (split.Length > 1)
                    {
                        return split[1];
                    }
                    return null;
                }
            }
        }

        public Hashtable Fields
        {
            get {
                return htFields;
            }
        }
    }
    class ImageParser
    {
        private Stream m_StrmSource = null;
        private string m_Boundary = null;
        private Hashtable headerFields = null;         

        public ImageParser(byte[] response)
        {            
            m_StrmSource = new MemoryStream();
            m_StrmSource.Write(response, 0, response.Length);
            m_StrmSource.Seek(0, SeekOrigin.Begin);
        }

        public Attachment GetNext()
        {
            Attachment atch = new Attachment();
            ReadHeader(atch);
            if (atch.Fields.Count == 0)
            {
                return null;
            }
            atch.Data = ReadContent();
            return atch;
        }

        private byte[] ReadLine()
		{
			MemoryStream strmLineBuf = new MemoryStream();
			byte      prevByte = 0;

			int currByteInt = m_StrmSource.ReadByte();
			while(currByteInt > -1){
				strmLineBuf.WriteByte((byte)currByteInt);

				// Line found
				if((prevByte == (byte)'\r' && (byte)currByteInt == (byte)'\n')){                    
                        //strmLineBuf.SetLength(strmLineBuf.Length - 2); // Remove <CRLF>                                        

					return strmLineBuf.ToArray();
				}
				
				// Store byte
				prevByte = (byte)currByteInt;

				// Read next byte
				currByteInt = m_StrmSource.ReadByte();				
			}

			// Line isn't terminated with <CRLF> and has some bytes left, return them.
			if(strmLineBuf.Length > 0){
				return strmLineBuf.ToArray();
			}

			return null;
		}

        private string ReadLineString()
        {
            byte[] line = ReadLine();
            if (line != null)
            {
                return UTF8Encoding.UTF8.GetString(line);                
            }
            else
            {
                return null;
            }
        }
        private string GetString(byte[] bytes)
        {
            if (bytes != null)
            {
                return UTF8Encoding.UTF8.GetString(bytes);
            }
            else
            {
                return null;
            }
        }

        public string GetSoapEnvelop()
        {
            string line = ReadLineString();
            headerFields = new Hashtable();
            while (line != null)
            {
                line = line.Trim();
                // End of header reached
                if (line == "")
                {
                    break;
                }


                string headerField = line;
                line = ReadLineString();

                while (line != null && (line.StartsWith("\t") || line.StartsWith(" ")))
                {
                    headerField += line;
                    line = ReadLineString();
                }

                string[] name_value = headerField.Split(new char[] { ':' }, 2);
                if (name_value.Length == 1)
                {
                    m_Boundary = headerField;
                }
                // There must be header field name and value, otherwise invalid header field
                if (name_value.Length == 2)
                {
                    headerFields.Add(name_value[0], name_value[1]);
                }
            }
            MemoryStream contentStrm = new MemoryStream();
            byte[] byteLine = ReadLine();
            line = GetString(byteLine);
            while (line != null)
            {
                line = line.Trim();
                if (line == m_Boundary)
                {
                    break;
                }                
                contentStrm.Write(byteLine, 0, byteLine.Length);
                byteLine = ReadLine();
                line = GetString(byteLine);
            }
            return GetString(contentStrm.ToArray());            
        }

        private void ReadHeader(Attachment atch)
        {
            string line = ReadLineString();            
            while (line != null)
            {
                line = line.Trim();
                // End of header reached
                if (line == "")
                {
                    break;
                }

                string headerField = line;
                line = ReadLineString();

                while (line != null && (line.StartsWith("\t") || line.StartsWith(" ")))
                {
                    headerField += line;
                    line = ReadLineString();
                }

                string[] name_value = headerField.Split(new char[] { ':' }, 2);
                // There must be header field name and value, otherwise invalid header field
                if (name_value.Length == 2)
                {
                    atch.AddField(name_value[0], name_value[1]);
                }
            }
        }

        private byte[] ReadContent()
        {
            MemoryStream contentStrm = new MemoryStream();
            byte[] byteLine = ReadLine();
            string line = GetString(byteLine);
            while (line != null)
            {
                line = line.Trim();
                if (line == m_Boundary)
                {
                    break;
                }
                contentStrm.Write(byteLine, 0, byteLine.Length);
                byteLine = ReadLine();
                line = GetString(byteLine);
            }
            return contentStrm.ToArray();   
        }
    }
}
