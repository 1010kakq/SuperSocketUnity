using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;

namespace SuperSocket.Client
{
    /// <summary>
    /// Represents a method that handles received packages in an <see cref="EasyClient{TReceivePackage}"/>.
    /// </summary>
    /// <typeparam name="TReceivePackage">The type of the received package.</typeparam>
    /// <param name="sender">The <see cref="EasyClient{TReceivePackage}"/> that received the package.</param>
    /// <param name="package">The received package.</param>
    /// <returns>A UniTask that represents the asynchronous handling operation.</returns>
    public delegate UniTask PackageHandler<TReceivePackage>(EasyClient<TReceivePackage> sender, TReceivePackage package)
        where TReceivePackage : class;
}
