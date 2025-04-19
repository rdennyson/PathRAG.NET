using MediatR;
using System.Runtime.CompilerServices;

namespace PathRAG.Core.Extensions;

public static class MediatRExtensions
{
    public static async IAsyncEnumerable<TResponse> CreateStream<TResponse>(
        this IMediator mediator,
        IStreamRequest<TResponse> request,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        // Get the handler directly from the service provider
        var handler = mediator.Send(request, cancellationToken);

        // The handler is a Task, but we need to return an IAsyncEnumerable
        // This is a simplified implementation that doesn't actually stream
        // In a real implementation, you would need to get the handler from the service provider
        var streamHandler = request as IStreamRequest<TResponse>;
        if (streamHandler != null)
        {
            var handlerType = typeof(IStreamRequestHandler<,>).MakeGenericType(request.GetType(), typeof(TResponse));
            var handleMethod = handlerType.GetMethod("Handle");

            if (handleMethod != null)
            {
                var result = handleMethod.Invoke(streamHandler, new object[] { request, cancellationToken });
                if (result is IAsyncEnumerable<TResponse> asyncEnumerable)
                {
                    await foreach (var item in asyncEnumerable.WithCancellation(cancellationToken))
                    {
                        yield return item;
                    }
                }
            }
        }
    }
}

public interface IStreamRequest<out TResponse> : IBaseRequest { }

public interface IStreamRequestHandler<in TRequest, TResponse> where TRequest : IStreamRequest<TResponse>
{
    IAsyncEnumerable<TResponse> Handle(TRequest request, CancellationToken cancellationToken);
}
