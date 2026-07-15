using System.Data;
using System.Text;
using System.Text.RegularExpressions;
using DocumentFormat.OpenXml.Packaging;
using ExcelDataReader;
using UglyToad.PdfPig;
using A = DocumentFormat.OpenXml.Drawing;

namespace Fri3ndsDrive.Api.Services
{
    public class TextExtractorService
    {
        public string ExtraerTexto(string rutaArchivo, string nombreOriginal)
        {
            try
            {
                string extension = Path.GetExtension(nombreOriginal).ToLower();

                return extension switch
                {
                    ".txt" => LeerTextoPlano(rutaArchivo),
                    ".md" => LeerTextoPlano(rutaArchivo),
                    ".csv" => LeerTextoPlano(rutaArchivo),
                    ".json" => LeerTextoPlano(rutaArchivo),
                    ".xml" => LeerTextoPlano(rutaArchivo),
                    ".html" => LimpiarHtml(LeerTextoPlano(rutaArchivo)),
                    ".htm" => LimpiarHtml(LeerTextoPlano(rutaArchivo)),
                    ".pdf" => LeerPdf(rutaArchivo),
                    ".docx" => LeerWord(rutaArchivo),
                    ".xlsx" => LeerExcel(rutaArchivo),
                    ".xls" => LeerExcel(rutaArchivo),
                    ".pptx" => LeerPowerPoint(rutaArchivo),
                    _ => ""
                };
            }
            catch
            {
                return "";
            }
        }

        private string LeerTextoPlano(string rutaArchivo)
        {
            return File.ReadAllText(rutaArchivo);
        }

        private string LimpiarHtml(string contenido)
        {
            string sinEtiquetas = Regex.Replace(contenido, "<.*?>", " ");
            string limpio = Regex.Replace(sinEtiquetas, @"\s+", " ");
            return limpio.Trim();
        }

        private string LeerPdf(string rutaArchivo)
        {
            var texto = new StringBuilder();

            using var documento = PdfDocument.Open(rutaArchivo);

            foreach (var pagina in documento.GetPages())
            {
                texto.AppendLine(pagina.Text);
            }

            return texto.ToString();
        }

        private string LeerWord(string rutaArchivo)
        {
            using var documento = WordprocessingDocument.Open(rutaArchivo, false);

            return documento.MainDocumentPart?.Document.Body?.InnerText ?? "";
        }

        private string LeerExcel(string rutaArchivo)
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

            var texto = new StringBuilder();

            using var stream = File.Open(rutaArchivo, FileMode.Open, FileAccess.Read);
            using var reader = ExcelReaderFactory.CreateReader(stream);

            DataSet result = reader.AsDataSet();

            foreach (DataTable tabla in result.Tables)
            {
                texto.AppendLine($"Hoja: {tabla.TableName}");

                foreach (DataRow fila in tabla.Rows)
                {
                    foreach (var celda in fila.ItemArray)
                    {
                        if (celda != null)
                        {
                            texto.Append(celda.ToString());
                            texto.Append(" | ");
                        }
                    }

                    texto.AppendLine();
                }
            }

            return texto.ToString();
        }

        private string LeerPowerPoint(string rutaArchivo)
        {
            var texto = new StringBuilder();

            using var presentacion = PresentationDocument.Open(rutaArchivo, false);

            var slides = presentacion.PresentationPart?.SlideParts;

            if (slides == null)
            {
                return "";
            }

            foreach (var slide in slides)
            {
                var textos = slide.Slide.Descendants<A.Text>();

                foreach (var t in textos)
                {
                    texto.AppendLine(t.Text);
                }
            }

            return texto.ToString();
        }
    }
}