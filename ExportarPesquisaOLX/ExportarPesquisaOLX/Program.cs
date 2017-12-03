using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace ExportarPesquisaOLX
{
    class Program
    {
        private struct Anuncio
        {
            public string Descricao;
            public string DetalhesEspecificos;
            public string Regiao;
            public float Preco;
            public string DetalhesCategoria;
            public DateTime DataInclusao;
            public string Link;
        }

        static void Main(string[] args)
        {
            try
            {
                Console.WriteLine("\t\t== Exportar Pesquisa OLX ==");

                string linkPartida = RetornarLinkPartida(args);

                Console.WriteLine("# Link de partida: {0}", linkPartida);
                Console.WriteLine("----------------------------------------");
                Console.WriteLine("> Iniciando colata dos dados.");

                List<Anuncio> anuncios = RetornarAnuncios(linkPartida);
                Salvar(anuncios);

                Console.WriteLine("> Processo finalizado.");
            }
            catch (Exception ex)
            {
                Console.WriteLine("[ERRO] Erro ao exportar o resultado da pesquisa. Texto erro: {0}", ex.ToString());
            }
            finally
            {
                Console.ReadKey();
            }
        }

        private static string RetornarLinkPartida(string[] args)
        {
            string linkPartida;

            try
            {
                linkPartida = args[0];

                if (string.IsNullOrEmpty(linkPartida))
                    throw new Exception("Parâmero Link Partida está vazio ou nulo.");
            }
            catch (Exception)
            {
                Console.WriteLine("[ERRO] Parâmetro do link de partida para a pesquisa não informado ou inválido.");

                Console.Write("Por favor, insira o link de partida para a pesquisa: ");
                linkPartida = Console.ReadLine();
            }

            return linkPartida;
        }

        private static List<Anuncio> RetornarAnuncios(string linkPartida)
        {
            List<Anuncio> anuncios = new List<Anuncio>();

            string linkPagina = linkPartida;

            do
            {
                Console.WriteLine("> Coletando em: {0}", linkPagina);
                string htmlPagina = GetStringAsync(linkPagina).Result;

                MatchCollection itensPesquisa = Regex.Matches(htmlPagina, @"(?s)<li class=""item"">[\r\n\s\t]*<a class=""OLXad-list-link"" lurker=""list_id"" data-lurker_list_id=""\d+"".+?</li>");

                foreach (Match item in itensPesquisa)
                    anuncios.Add(RetornarAnuncio(item.Value));

                linkPagina = Regex.Match(htmlPagina, @"(?<=<a class=""link""[\s\t]+rel=""next""[\s\t]+href="").+?(?="")").Value;
            } while (!string.IsNullOrEmpty(linkPagina));

            return anuncios;
        }

        private static void Salvar(List<Anuncio> anuncios)
        {
            Console.WriteLine("> Salvando resultado.");
            string conteudoArquivoCsv = RetornarConteudoArquivoCsv(anuncios);

            string destino = $@"{AppDomain.CurrentDomain.BaseDirectory}resultados\";
            Directory.CreateDirectory(destino);

            string arquivo = $"{destino}{DateTime.Now.ToString("yyyy-MM-dd-HHmmss")}_resultado.csv";
            File.WriteAllText(arquivo, conteudoArquivoCsv, Encoding.UTF8);

            Console.WriteLine("> Arquivo salvo em: {0}", arquivo);
        }

        private static string RetornarConteudoArquivoCsv(List<Anuncio> anuncios)
        {
            StringBuilder conteudoArquivo = new StringBuilder();

            conteudoArquivo.AppendFormat("Descrição;Detalhes Específicos;Região;Preço;Categoria;Inclusão;Link{0}", Environment.NewLine);
            foreach (Anuncio anuncio in anuncios)
            {
                conteudoArquivo.AppendFormat("{0};{1};{2};{3};{4};{5};{6}{7}",
                    anuncio.Descricao,
                    anuncio.DetalhesEspecificos,
                    anuncio.Regiao,
                    anuncio.Preco,
                    anuncio.DetalhesCategoria,
                    anuncio.DataInclusao,
                    anuncio.Link,
                    Environment.NewLine);
            }

            return conteudoArquivo.ToString();
        }

        private static Anuncio RetornarAnuncio(string linhaAnuncio)
        {
            Anuncio informacoes = new Anuncio();
            informacoes.Descricao = RetornarDescricao(linhaAnuncio);
            informacoes.DetalhesEspecificos = RetornarDetalhesEspecificos(linhaAnuncio);
            informacoes.Regiao = RetornarRegiao(linhaAnuncio);
            informacoes.Preco = RetornarPreco(linhaAnuncio);
            informacoes.DetalhesCategoria = RetornarDetalhesCategoria(linhaAnuncio);
            informacoes.DataInclusao = RetornarDataInclusao(linhaAnuncio);
            informacoes.Link = RetornarLink(linhaAnuncio);

            return informacoes;
        }

        private static string RetornarDescricao(string linhaAnuncio)
        {
            return RetornarValorMatch(linhaAnuncio, @"(?s)(?<=<h3 class=""OLXad-list-title mb5px"">).+?(?=</h3>)");
        }

        private static string RetornarDetalhesEspecificos(string linhaAnuncio)
        {
            return RetornarValorMatch(linhaAnuncio, @"(?s)(?<=<p class=""text detail-specific mt5px"">).+?(?=</p>)");
        }

        private static string RetornarRegiao(string linhaAnuncio)
        {
            return RetornarValorMatch(linhaAnuncio, @"(?s)(?<=<p class=""text detail-region"">).+?(?=</p>)");
        }

        private static string RetornarDetalhesCategoria(string linhaAnuncio)
        {
            string htmlDetalhesCategoria = RetornarValorMatch(linhaAnuncio, @"(?s)(?<=<p class=""text detail-category"">).+?(?=</p>)");
            string detalhesCategoria = Regex.Replace(htmlDetalhesCategoria, @"<span class=""pro"">", " ").Replace("</span>", "");
            return detalhesCategoria;
        }

        private static DateTime RetornarDataInclusao(string linhaAnuncio)
        {
            string htmlDataInclusao = RetornarValorMatch(linhaAnuncio, @"(?s)<div class=""col-4"">.+?</div>");
            string textoDataInclusao = htmlDataInclusao.Replace("</p>", " ");
            textoDataInclusao = Regex.Replace(textoDataInclusao, "<.+?>", "").Replace("</span>", "");

            DateTime dataInclusao;

            if (textoDataInclusao.Contains("Hoje"))
                dataInclusao = DateTime.Parse(textoDataInclusao.Replace("Hoje", DateTime.Today.ToString("dd/MM/yyyy")));
            else if (textoDataInclusao.Contains("Ontem"))
                dataInclusao = DateTime.Parse(textoDataInclusao.Replace("Ontem", DateTime.Today.AddDays(-1).ToString("dd/MM/yyyy")));
            else
            {
                textoDataInclusao = FormatarData(textoDataInclusao);
                dataInclusao = DateTime.Parse(textoDataInclusao);
            }

            return dataInclusao;
        }

        private static string FormatarData(string textoDataInclusao)
        {
            string mes = Regex.Match(textoDataInclusao, @"(?<=\d+) \w{3}\b").Value;
            string mesformatado = FormatarMes(mes);
            return textoDataInclusao.Replace(mes, mesformatado);
        }

        private static string FormatarMes(string mes)
        {
            string numeroMes;

            switch (mes)
            {
                case " Jan":
                    numeroMes = "01"; break;
                case " Fev":
                    numeroMes = "02"; break;
                case " Mar":
                    numeroMes = "03"; break;
                case " Abr":
                    numeroMes = "04"; break;
                case " Mai":
                    numeroMes = "05"; break;
                case " Jun":
                    numeroMes = "06"; break;
                case " Jul":
                    numeroMes = "07"; break;
                case " Ago":
                    numeroMes = "08"; break;
                case " Set":
                    numeroMes = "09"; break;
                case " Out":
                    numeroMes = "10"; break;
                case " Nov":
                    numeroMes = "11"; break;
                case " Dez":
                    numeroMes = "12"; break;
                default:
                    throw new Exception($"Erro ao formatar mês. Texto do mês: {mes}");
            }

            return $"/{numeroMes}/{DateTime.Today.Year}";
        }

        private static float RetornarPreco(string linhaAnuncio)
        {
            string textoPreco = RetornarValorMatch(linhaAnuncio, @"(?s)(?<=<p class=""OLXad-list-price"">[\r\n\s\t]*R\$[\s\t]*).+?(?=</p>)");
            float preco = 0;

            try
            {
                preco = float.Parse(textoPreco);
            }
            catch (Exception)
            {
            }

            return preco;
        }

        private static string RetornarLink(string linhaAnuncio)
        {
            return RetornarValorMatch(linhaAnuncio, @"(?<=name=""\d+"" id=""\d+"" href="").+?(?="")");
        }

        private static string RetornarValorMatch(string entrada, string pattern)
        {
            return WebUtility.HtmlDecode(Regex.Match(entrada, pattern).Value.Trim().Replace("\t", "").Replace("\n", "").Replace("\r", ""));
        }

        private static async Task<string> GetStringAsync(string uri)
        {
            string htmlPagina = null;

            using (HttpClient httpClient = new HttpClient())
            {
                httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/62.0.3202.94 Safari/537.36");
                htmlPagina = await httpClient.GetStringAsync(uri);
            }

            return htmlPagina;
        }
    }
}