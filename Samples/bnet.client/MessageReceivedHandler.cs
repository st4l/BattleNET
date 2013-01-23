// ----------------------------------------------------------------------------------------------------
// <copyright file="MessageReceivedHandler.cs" company="Me">Copyright (c) 2012 St4l.</copyright>
// ----------------------------------------------------------------------------------------------------
namespace BNet.Client
{
    /// <summary>
    ///     Represents the method that will handle a MessageReceived event.
    /// </summary>
    /// <param name="sender">The source of the event.</param>
    /// <param name="e">
    ///     An <see cref="MessageReceivedHandlerArgs" /> that contains
    ///     data representing the received message.
    /// </param>
    /// <filterpriority>1</filterpriority>
    public delegate void MessageReceivedHandler(object sender, MessageReceivedHandlerArgs e);
}
