﻿Imports iTextSharp.text.pdf
Imports iTextSharp.text
Imports System.IO
Imports System.Text
Imports System.Xml
Imports System.Text.RegularExpressions
Imports System.Windows.Forms
Imports System.Drawing.Imaging

Public Class iTextSharpUtil

  Public Shared mBookmarkList As New ArrayList

  Public Shared Function GetPDFPageCount(ByVal filepath As String, Optional ByVal userPassword As String = "") As Integer

    Dim oPdfReader As PdfReader
    If userPassword <> "" Then
      Dim encoding As New System.Text.ASCIIEncoding()
      oPdfReader = New PdfReader(filepath, encoding.GetBytes(userPassword))
    Else
      oPdfReader = New PdfReader(filepath)
    End If
    Dim page_count As Integer = oPdfReader.NumberOfPages
    oPdfReader.Close()
    Return page_count
  End Function

  Public Shared Function GetPDFPageSize(ByVal filepath As String, ByVal pageNumber As Integer, Optional ByVal userPassword As String = "") As Drawing.Size
    Dim oPdfReader As PdfReader
    If userPassword <> "" Then
      Dim encoding As New System.Text.ASCIIEncoding()
      oPdfReader = New PdfReader(filepath, encoding.GetBytes(userPassword))
    Else
      oPdfReader = New PdfReader(filepath)
    End If
    Dim page_count As Integer = oPdfReader.NumberOfPages
    If pageNumber >= 1 And pageNumber <= page_count Then
      Dim rect As iTextSharp.text.Rectangle = oPdfReader.GetPageSize(pageNumber)
      GetPDFPageSize.Height = rect.Height
      GetPDFPageSize.Width = rect.Width
    End If
    oPdfReader.Close()
  End Function

  Public Shared Function GetOptimalDPI(ByVal filepath As String, ByVal pageNumber As Integer, ByRef oPictureBox As PictureBox, Optional ByVal userPassword As String = "") As Integer
    GetOptimalDPI = 0
    Dim pageSize As Drawing.Size = GetPDFPageSize(filepath, pageNumber, userPassword)
    If pageSize.Width > 0 And pageSize.Height > 0 Then
      Dim HScale As Single = oPictureBox.Width / (pageSize.Width)
      Dim VScale As Single = oPictureBox.Height / (pageSize.Height)
      If HScale < VScale Then
        GetOptimalDPI = Math.Floor(72 * HScale)
      Else
        GetOptimalDPI = Math.Floor(72 * VScale)
      End If
    End If
  End Function

  Public Shared Function BuildBookmarkTreeFromPDF(ByVal FileName As String, ByVal TreeNodes As TreeNodeCollection, Optional ByVal userPassword As String = "") As Boolean
    TreeNodes.Clear()
    Dim oPdfReader As PdfReader
    If userPassword <> "" Then
      Dim encoding As New System.Text.ASCIIEncoding()
      oPdfReader = New PdfReader(FileName, encoding.GetBytes(userPassword))
    Else
      oPdfReader = New PdfReader(FileName)
    End If

    Dim arList As ArrayList = New ArrayList()
    arList = SimpleBookmark.GetBookmark(oPdfReader)
    oPdfReader.Close()
    If Nothing Is arList Then
      Return False
    End If
    Using ms As New MemoryStream()
      SimpleBookmark.ExportToXML(arList, ms, "UTF-8", False)
      ms.Position = 0
      Using xr As XmlReader = XmlReader.Create(ms)
        xr.MoveToContent()
        Dim page As String = Nothing
        Dim text As String = Nothing
        Dim CurrentNode As New TreeNode
        CurrentNode = TreeNodes.Add("Bookmarks")
        Dim re As New Regex("^\d+")
        While xr.Read()
          If xr.NodeType = XmlNodeType.Element AndAlso xr.Name = "Title" AndAlso xr.IsStartElement() Then
            If Not Nothing = xr.GetAttribute("Page") Then
              page = re.Match(xr.GetAttribute("Page")).Captures(0).Value
            End If
            xr.Read()
            If xr.NodeType = XmlNodeType.Text Then
              text = xr.Value.Trim()
              CurrentNode = CurrentNode.Nodes.Add(page, text, page)
            End If
          End If
          If xr.NodeType = XmlNodeType.EndElement AndAlso xr.Name = "Title" Then
            If Not Nothing Is CurrentNode.Parent Then
              CurrentNode = CurrentNode.Parent
            End If
          End If
        End While
      End Using
    End Using
    Return True
  End Function

  Public Shared Function BuildHTMLBookmarks(ByVal FileName As String, Optional ByVal userPassword As String = "") As String
    Dim oPdfReader As PdfReader

    If userPassword <> "" Then
      Dim encoding As New System.Text.ASCIIEncoding()
      oPdfReader = New PdfReader(FileName, encoding.GetBytes(userPassword))
    Else
      oPdfReader = New PdfReader(FileName)
    End If

    Dim PageCount As Integer = oPdfReader.NumberOfPages
    oPdfReader.Close()
    Dim arList As ArrayList = New ArrayList()
    arList = SimpleBookmark.GetBookmark(oPdfReader)

    If Nothing Is arList Then
BetterThanNothing:
      BuildHTMLBookmarks = "<ul>"
      For i As Integer = 1 To PageCount
        BuildHTMLBookmarks &= "<li><a href=""javascript:changeImage('images/page" & i & ".png')"">Page " & i & "</a></li>"
      Next
      BuildHTMLBookmarks &= "</ul>"
      Exit Function
    End If

    Using ms As New MemoryStream()
      SimpleBookmark.ExportToXML(arList, ms, "UTF-8", False)
      ms.Position = 0
      Dim sb As New StringBuilder()
      Try
        Using xr As XmlReader = XmlReader.Create(ms)
          xr.MoveToContent()
          Dim page As String = Nothing
          ' save page number for link
          Dim text As String = Nothing
          ' link text from PDF bookmark
          ' see notes below for actual link format
          Dim format As String = "<li><a href=""javascript:changeImage('images/page{0}.png')"">{1}</a></li>"
          ' extract page number from 'Page' attribute
          Dim re As New Regex("^\d+")
          While xr.Read()
            If xr.NodeType = XmlNodeType.Element AndAlso xr.Name = "Title" AndAlso xr.IsStartElement() Then
              sb.Append("<ul>")
              ' in production app separate steps:
              ' if GetAttribute() returns null if attr not found,
              ' which makes Regex.Match choke and die
              page = re.Match(xr.GetAttribute("Page")).Captures(0).Value
              xr.Read()
              ' hyperlink text
              If xr.NodeType = XmlNodeType.Text Then
                text = xr.Value.Trim()
                text = Web.HttpUtility.HtmlEncode(text)
                ' in production app verify page & text
                ' aren't empty before appending
                sb.Append([String].Format(format, page, text))
              End If
            End If
            ' close current (HTML) list
            If xr.NodeType = XmlNodeType.EndElement AndAlso xr.Name = "Title" Then
              sb.Append("</ul>")
            End If
          End While
          Return sb.ToString()
        End Using
      Catch ex As Exception
        GoTo BetterThanNothing
      End Try

    End Using

  End Function

  Public Shared Function GraphicListToPDF(ByVal psFilenames As String() _
                                          , ByVal outputFileName As String _
                                          , ByVal psPageSize As iTextSharp.text.Rectangle _
                                          , Optional ByVal language As String = "" _
                                          , Optional ByVal StartPage As Integer = 0 _
                                          , Optional ByVal EndPage As Integer = 0 _
                                          , Optional ByVal UserPassword As String = "" _
                                          , Optional ByVal OwnerPassword As String = "")

    Dim StatusDialog As New ImportProgress
    StatusDialog.TopMost = True
    StatusDialog.Show()
    Dim document As iTextSharp.text.Document
    document = New Document(psPageSize, 0, 0, 0, 0)

    Try
      Dim writer As PdfWriter = PdfWriter.GetInstance(document, New FileStream(outputFileName, FileMode.Create))
      If UserPassword <> "" Or OwnerPassword <> "" Then
        writer.SetEncryption(PdfWriter.STRENGTH128BITS, UserPassword, OwnerPassword, PdfWriter.AllowCopy Or PdfWriter.AllowPrinting)
      End If
      document.Open()
      Dim cb As PdfContentByte = writer.DirectContent
      Dim fileNumber As Integer = 0
      For Each psFileName As String In psFilenames
        Dim ProgressIncrement As Integer = 100 / psFilenames.Length
        fileNumber += 1
        StatusDialog.UpdateProgress("Processing file " & fileNumber & "/" & psFilenames.Length, 0)
        Application.DoEvents()
        Dim bm As New System.Drawing.Bitmap(psFileName)
        Dim total As Integer = bm.GetFrameCount(FrameDimension.Page)

        If StartPage = 0 And EndPage = 0 Then
          StartPage = 1
          EndPage = total
        End If

        For k As Integer = StartPage To EndPage
          StatusDialog.UpdateProgress("Processing page " & k & "/" & EndPage & " for file " & fileNumber & "/" & psFilenames.Length, ProgressIncrement / EndPage)
          Application.DoEvents()
          bm.SelectActiveFrame(FrameDimension.Page, k - 1)
          'Auto Rotate the page if needed
          If (psPageSize.Height > psPageSize.Width And bm.Width > bm.Height) _
          Or (psPageSize.Width > psPageSize.Height And bm.Height > bm.Width) Then
            document.SetPageSize(psPageSize.Rotate)
          Else
            document.SetPageSize(psPageSize)
          End If
          document.NewPage()
          Dim img As iTextSharp.text.Image
          img = iTextSharp.text.Image.GetInstance(bm, bm.RawFormat)
          Dim Xpercent As Single = document.PageSize.Width / img.Width
          Dim Ypercent As Single = document.PageSize.Height / img.Height
          Dim ScalePercentage As Single
          If Xpercent < Ypercent Then
            ScalePercentage = Xpercent
          Else
            ScalePercentage = Ypercent
          End If
          img.ScalePercent(ScalePercentage * 100)
          Dim xPos As Integer = (document.PageSize.Width - (img.Width * ScalePercentage)) / 2
          Dim yPos As Integer = (document.PageSize.Height - (img.Height * ScalePercentage)) / 2
          img.SetAbsolutePosition(xPos, yPos)
          Try
            If language <> "" Then
              StatusDialog.UpdateProgress("OCR reading page " & k & "/" & EndPage & " for file " & fileNumber & "/" & psFilenames.Length, 0)
              Application.DoEvents()
              Dim bf As BaseFont = BaseFont.CreateFont(BaseFont.HELVETICA, BaseFont.WINANSI, BaseFont.NOT_EMBEDDED)
              cb.BeginText()
              Dim indexList As List(Of PDFWordIndex)
              indexList = TesseractOCR.GetPDFIndex(bm, language)

              StatusDialog.UpdateProgress("Adding search text to page " & k & "/" & EndPage & " for file " & fileNumber & "/" & psFilenames.Length, 0)
              Application.DoEvents()
              For Each item As PDFWordIndex In indexList
                Dim fontSize As Single = item.FontSize
                Dim text As String = item.Text
                cb.SetColorFill(Color.WHITE) 'make text invisible in background
                'Must convert image x,y (x units per inch) to PDF x,y (72 units per inch)
                'Must know PDF page size to calculate the scale factor
                'Must invert Y coordinate so we go top -> bottom
                Dim x As Integer = (item.X * ScalePercentage) + xPos
                Dim y As Integer = (item.Y * ScalePercentage) + yPos
                y = (document.PageSize.Height - y) - fontSize
                'Keep adjusting the font size until the text is the same width as the word rectangle 
                Dim desiredWidth As Integer = Math.Ceiling(item.Width * ScalePercentage)
                Dim desiredHeight As Integer = Math.Ceiling(item.Height * ScalePercentage)
                Dim renderFontWidth As Integer = bf.GetWidthPoint(text, fontSize)
                While renderFontWidth < desiredWidth
                  fontSize += 0.5F
                  renderFontWidth = bf.GetWidthPoint(text, fontSize)
                End While
                cb.SetFontAndSize(bf, fontSize)
                y = y - (fontSize - item.FontSize) / 2
                cb.ShowTextAlignedKerned(Element.ALIGN_JUSTIFIED_ALL, text, x, y, 0)
              Next
              cb.EndText()
            End If
          Catch ex As Exception
            MsgBox(ex.ToString)
          End Try
          Try
            StatusDialog.UpdateProgress("Adding image to PDF of page " & k & "/" & EndPage & " for file " & fileNumber & "/" & psFilenames.Length, 0)
            Application.DoEvents()
            cb.AddImage(img)
          Catch ex As Exception
            MsgBox(ex.ToString)
          End Try
        Next
        bm.Dispose()
      Next
      document.Close()
      StatusDialog.Close()
      GraphicListToPDF = outputFileName
    Catch de As Exception
      StatusDialog.Close()
      MsgBox(de.Message)
      'Console.[Error].WriteLine(de.Message)
      'Console.[Error].WriteLine(de.StackTrace)
      GraphicListToPDF = ""
    End Try
  End Function

  Public Shared Function IsEncrypted(ByVal pdfFileName As String) As Boolean
    IsEncrypted = False
    Try
      Dim oPDFReader As New PdfReader(pdfFileName)
      oPDFReader.Close()
    Catch ex As BadPasswordException
      IsEncrypted = True
    End Try
  End Function

  Public Shared Function IsPasswordValid(ByVal pdfFileName As String, ByVal Password As String) As Boolean
    IsPasswordValid = False
    Try
      Dim encoding As New System.Text.ASCIIEncoding()
      Dim oPDFReader As New PdfReader(pdfFileName, encoding.GetBytes(Password))
      oPDFReader.Close()
      IsPasswordValid = True
    Catch ex As BadPasswordException
      'Authentication Failed
    End Try
  End Function

End Class