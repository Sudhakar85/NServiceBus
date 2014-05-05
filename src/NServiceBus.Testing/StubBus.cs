﻿namespace NServiceBus.Testing
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    public class StubBus : IBus
    {
        private readonly IMessageCreator messageCreator;
        private readonly IDictionary<string, string> outgoingHeaders = new Dictionary<string, string>();
        private readonly List<ActualInvocation> actualInvocations = new List<ActualInvocation>();
        private readonly TimeoutManager timeoutManager = new TimeoutManager();

        public void ValidateAndReset(IEnumerable<IExpectedInvocation> expectedInvocations)
        {
            expectedInvocations.ToList().ForEach(e => e.Validate(actualInvocations.ToArray()));

            actualInvocations.Clear();
        }

        public object PopTimeout()
        {
            return timeoutManager.Pop();
        }

        public StubBus(IMessageCreator creator)
        {
            messageCreator = creator;
        }

        public void Publish<T>()
        {
            ProcessInvocation(typeof(PublishInvocation<>), CreateInstance<T>());
        }

        public void Publish<T>(T message)
        {
            ProcessInvocation(typeof(PublishInvocation<>), message);
        }

        public void Publish<T>(Action<T> messageConstructor)
        {
            Publish(messageCreator.CreateInstance(messageConstructor));
        }

        public void Subscribe(Type messageType)
        {
            throw new NotSupportedException();
        }

        public void Subscribe<T>()
        {
            throw new NotSupportedException();
        }

        public void Subscribe(Type messageType, Predicate<object> condition)
        {
            throw new NotSupportedException();
        }

        public void Subscribe<T>(Predicate<T> condition)
        {
            throw new NotSupportedException();
        }

        public void Unsubscribe(Type messageType)
        {
            throw new NotSupportedException();
        }

        public void Unsubscribe<T>()
        {
            throw new NotSupportedException();
        }

        public ICallback SendLocal(object message)
        {
            return ProcessInvocation(typeof(SendLocalInvocation<>), message);
        }

        public ICallback SendLocal<T>(Action<T> messageConstructor)
        {
            return SendLocal(messageCreator.CreateInstance(messageConstructor));
        }

        
        public ICallback Send(object message)
        {
            return Send(Address.Undefined, message);
        }

        public ICallback Send<T>(Action<T> messageConstructor)
        {
            return Send(string.Empty, messageCreator.CreateInstance(messageConstructor));
        }

        public ICallback Send(string destination, object message)
        {
            if (destination == string.Empty)
                return Send(Address.Undefined, message);

            return Send(Address.Parse(destination), message);
        }

        public ICallback Send(Address address, object message)
        {
            return Send(address, String.Empty, message);
        }

        public ICallback Send<T>(string destination, Action<T> messageConstructor)
        {
            return Send(destination, messageCreator.CreateInstance(messageConstructor));
        }

        public ICallback Send<T>(Address address, Action<T> messageConstructor)
        {
            return Send(address, messageCreator.CreateInstance(messageConstructor));
        }


        public ICallback Send(string destination, string correlationId, object message)
        {
            if (destination == string.Empty)
                return Send(Address.Undefined, correlationId, message);

            return Send(Address.Parse(destination), correlationId, message);
        }

        public ICallback Send(Address address, string correlationId, object message)
        {
            if (address != Address.Undefined && correlationId != string.Empty)
            {
                var d = new Dictionary<string, object> {{"Address", address}, {"CorrelationId", correlationId}};
                return ProcessInvocation(typeof(ReplyToOriginatorInvocation<>), d, message);
            }

            if (address != Address.Undefined && correlationId == string.Empty)
                return ProcessInvocation(typeof(SendToDestinationInvocation<>), new Dictionary<string, object> { { "Address", address } }, message);

            return ProcessInvocation(typeof(SendInvocation<>), message);
        }

        public ICallback Send<T>(string destination, string correlationId, Action<T> messageConstructor)
        {
            return Send(destination, correlationId, messageCreator.CreateInstance(messageConstructor));
        }

        public ICallback Send<T>(Address address, string correlationId, Action<T> messageConstructor)
        {
            return Send(address, correlationId, messageCreator.CreateInstance(messageConstructor));
        }

        public ICallback SendToSites(IEnumerable<string> siteKeys, object message)
        {
            return ProcessInvocation(typeof(SendToSitesInvocation<>), new Dictionary<string, object> { { "Value", siteKeys } }, message);
        }

        public ICallback Defer(TimeSpan delay, object messages)
        {
            return ProcessDefer<TimeSpan>(delay, messages);
        }

        public ICallback Defer(DateTime processAt, object message)
        {
            return ProcessDefer<DateTime>(processAt, message);
        }

        public void Reply(object message)
        {
            ProcessInvocation(typeof(ReplyInvocation<>), message);
        }

        public void Reply<T>(Action<T> messageConstructor)
        {
            Reply(messageCreator.CreateInstance(messageConstructor));
        }

        public void Return<T>(T errorEnum)
        {
            actualInvocations.Add(new ReturnInvocation<T> { Value = errorEnum});
        }

        public void HandleCurrentMessageLater()
        {
            actualInvocations.Add(new HandleCurrentMessageLaterInvocation<object>());
        }

        public void ForwardCurrentMessageTo(string destination)
        {
            actualInvocations.Add(new ForwardCurrentMessageToInvocation { Value = destination });
        }

        public void DoNotContinueDispatchingCurrentMessageToHandlers()
        {
            actualInvocations.Add(new DoNotContinueDispatchingCurrentMessageToHandlersInvocation<object>());
        }

        public IDictionary<string, string> OutgoingHeaders
        {
            get { return outgoingHeaders; }
        }

        public IMessageContext CurrentMessageContext { get; set; }

        public IInMemoryOperations InMemory
        {
            get { throw new NotImplementedException(); }
        }

        public void Shutdown()
        {
            throw new NotImplementedException();
        }

        public IBus Start(Action startupAction)
        {
            throw new NotImplementedException();
        }

        public IBus Start()
        {
            throw new NotImplementedException();
        }

#pragma warning disable 67
        public event EventHandler Started;
#pragma warning restore 67

        public T CreateInstance<T>()
        {
            return messageCreator.CreateInstance<T>();
        }

        public T CreateInstance<T>(Action<T> action)
        {
            return messageCreator.CreateInstance(action);
        }

        public object CreateInstance(Type messageType)
        {
            return messageCreator.CreateInstance(messageType);
        }

        private ICallback ProcessInvocation(Type genericType, object message)
        {
            return ProcessInvocation(genericType, new Dictionary<string, object>(), message);
        }

        ICallback ProcessInvocation(Type genericType, Dictionary<string, object> others, object message)
        {
            var messageType = GetMessageType(message);
            var invocationType = genericType.MakeGenericType(messageType);
            return ProcessInvocationWithBuiltType(invocationType, others, message);
        }

        ICallback ProcessInvocation<K>(Type dualGenericType, Dictionary<string, object> others, object message)
        {
            var invocationType = dualGenericType.MakeGenericType(GetMessageType(message), typeof(K));
            return ProcessInvocationWithBuiltType(invocationType, others, message);
        }

        private ICallback ProcessInvocationWithBuiltType(Type builtType, Dictionary<string, object> others, object message)
        {
            if (message == null)
                throw new NullReferenceException("message is null.");

            var invocation = Activator.CreateInstance(builtType) as ActualInvocation;

            builtType.GetProperty("Message").SetValue(invocation, message, null);

            foreach (var kv in others)
                builtType.GetProperty(kv.Key).SetValue(invocation, kv.Value, null);

            actualInvocations.Add(invocation);

            return null;
        }

        private Type GetMessageType(object message)
        {
            if (message.GetType().FullName.EndsWith("__impl"))
            {
                var name = message.GetType().FullName.Replace("__impl", "").Replace("\\","");
                foreach (var i in message.GetType().GetInterfaces())
                    if (i.FullName == name)
                        return i;
            }

            return message.GetType();
        }

        private ICallback ProcessDefer<T>(object delayOrProcessAt, object message)
        {
            timeoutManager.Push(delayOrProcessAt, message);
            return ProcessInvocation<T>(typeof(DeferMessageInvocation<,>), new Dictionary<string, object> { { "Value", delayOrProcessAt } }, message);
        }
    }
}
