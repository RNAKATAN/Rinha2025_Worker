namespace Rinha2025_Worker.Contratos
{
    public interface IHttpFacade<T> where T : class
    {
        Task<T> ExecutaTarefa(HttpRequestMessage httpRequestMessage);
    }
}
