using System.Linq;
using System.Collections.Generic;
namespace {{rootnamespace}}
{
	[System.Diagnostics.DebuggerDisplay("{" + nameof(Format) + "(),nq}")]
	/// <summary>
	/// Represents response from API server.
	/// </summary>
	public readonly struct Response
	{
		public Response(int statusCode, string message) : this(null, statusCode, message) { }

		/// <summary>
		/// Initializes a new instance of the <see cref="Response"/> struct.
		/// </summary>
		/// <param name="success">if set to <c>true</c> [success].</param>
		/// <param name="statusCode">The status code.</param>
		/// <param name="message">The message.</param>
		public Response(object data, int statusCode, string message)
		{
			Data = data;
			Message = message;
			StatusCode = statusCode;
			Succeeded = statusCode >= 200 && statusCode <= 299;
		}

		/// <summary>
		/// Gets the HTTP status code.
		/// </summary>
		/// <value>The HTTP status code.</value>
		public int StatusCode { get; }

		/// <summary>
		/// Gets the error message.
		/// </summary>
		/// <value>The message.</value>
		public string Message { get; }

		public object Data { get; }

		/// <summary>
		/// Gets a value indicating whether the corresponding request is succeeded.
		/// </summary>
		/// <value><c>true</c> if succeeded; otherwise, <c>false</c>.</value>
		public bool Succeeded { get; }

		public T GetValue<T>() => (T)Data;

		/// <summary>
		/// Convert the value of this instance to a <see cref="System.String"/>.
		/// </summary>
		/// <returns>
		/// A <see cref="System.String" /> that represents this instance.
		/// </returns>
		public override string ToString() => Message;

		internal string Format() => $"({StatusCode}): {Message}".Trim(' ', ':');

		#region operators

		public static implicit operator bool(Response x) => x.Succeeded;

		#endregion operators
	}

	[System.Diagnostics.DebuggerDisplay("{" + nameof(Format) + "(),nq}")]
	/// <summary>
	/// Represents response data from API server.
	/// </summary>
	/// <typeparam name="T"></typeparam>
	public readonly struct Response<T>
	{
		/// <summary>
		/// Initializes a new instance of the <see cref="Response{T}"/> struct.
		/// </summary>
		/// <param name="data">The data.</param>
		/// <param name="statusCode">The status code.</param>
		/// <param name="message">The message.</param>
		public Response(T data, int statusCode = 200, string message = default)
			: this(data, Response.IsGood(statusCode), statusCode, message) { }

		/// <summary>
		/// Initializes a new instance of the <see cref="Response{T}"/> struct.
		/// </summary>
		/// <param name="data">The data.</param>
		/// <param name="success">if set to <c>true</c> [success].</param>
		/// <param name="status">The status.</param>
		/// <param name="message">The message.</param>
		public Response(T data, bool success, int status, string message)
		{
			Data = data;
			Succeeded = success;
			StatusCode = status;
			Message = message;
		}

		/// <summary>
		/// Gets the data.
		/// </summary>
		/// <value>The data.</value>
		public T Data { get; }

		/// <summary>
		/// Gets the HTTP status code.
		/// </summary>
		/// <value>The HTTP status code.</value>
		public int StatusCode { get; }

		/// <summary>
		/// Gets the error message.
		/// </summary>
		/// <value>The message.</value>
		public string Message { get; }

		/// <summary>
		/// Gets a value indicating whether the corresponding request is succeeded.
		/// </summary>
		/// <value><c>true</c> if succeeded; otherwise, <c>false</c>.</value>
		public bool Succeeded { get; }

		/// <summary>
		/// Gets a value indicating whether the corresponding request is failed.
		/// </summary>
		/// <value><c>true</c> if failed; otherwise, <c>false</c>.</value>
		public bool Failed
		{
			get => Succeeded == false;
		}

		/// <summary>
		/// Convert the value of this instance to a <see cref="System.String"/>.
		/// </summary>
		/// <returns>
		/// A <see cref="System.String" /> that represents this instance.
		/// </returns>
		public override string ToString() => Message;

		/// <summary>
		/// Returns a list of errors returned by the server.
		/// </summary>
		public IEnumerable<ResponseError> GetErrors() => Response.ParseErrors(Message);

		internal string Format() => $"({StatusCode}): {Message}".Trim(' ', ':');

		#region operators

		public static implicit operator T(Response<T> x) => x.Data;

		public static explicit operator bool(Response<T> x) => x.Succeeded;

		public static implicit operator Response(Response<T> x) => new Response(x.Succeeded, x.StatusCode, x.Message);

		#endregion operators
	}

	/// <summary>
	/// Represents an error returned by a server.
	/// </summary>
	public readonly struct ResponseError
	{
		/// <summary>
		/// /// Initializes a new instance of the <see cref="ResponseError"/> struct.
		/// </summary>
		/// <param name="message">The error message.</param>
		/// <param name="code">The error code.</param>
		public ResponseError(string message, int code)
		{
			ErrorCode = code;
			Message = message;
		}

		/// <summary>
		/// The error code.
		/// </summary>
		public int ErrorCode { get; }

		/// <summary>
		/// The error message.
		/// </summary>
		public string Message { get; }
	}
}