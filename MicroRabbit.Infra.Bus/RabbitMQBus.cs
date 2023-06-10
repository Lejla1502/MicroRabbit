using MediatR;
using MicroRabbit.Domain.Core.Bus;
using MicroRabbit.Domain.Core.Commands;
using MicroRabbit.Domain.Core.Events;
using Newtonsoft.Json;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Metadata.Ecma335;
using System.Text;
using System.Threading.Tasks;

namespace MicroRabbit.Infra.Bus
{
    public sealed class RabbitMQBus : IEventBus
    {
        private readonly IMediator _mediator;
        //subscription handlers which know about whihc subscriptions are tied to which handlers and events
        private readonly Dictionary<string, List<Type>> _handlers;
        private readonly List<Type> _eventTypes;

        public RabbitMQBus(IMediator mediator)
        {
            _mediator = mediator;
            _handlers = new Dictionary<string, List<Type>>();
            _eventTypes = new List<Type>();
        }

        public Task SendCommand<T>(T command) where T : Command
        {
            return _mediator.Send(command);
        }
        public void Publish<T>(T @event) where T : Event
        {
            var factory = new ConnectionFactory() { HostName = "localhost"};
            using(var connection = factory.CreateConnection())
            using(var channel = connection.CreateModel())
            {
                var eventName = @event.GetType().Name;
                channel.QueueDeclare(eventName, false, false, false, null);

                string message = JsonConvert.SerializeObject(@event);
                var body = Encoding.UTF8.GetBytes(message);

                channel.BasicPublish("", eventName, null, body);
            }
        }

        public void Subscribe<T, TH>()
            where T : Event
            where TH : IEventHandler<T>
        {
            var eventName = typeof(T).Name;
            var handlerType = typeof(TH);

            //if events don't contain the given type, add that type to the list
            if(!_eventTypes.Contains(typeof(T)))
                _eventTypes.Add(typeof(T));

            //if dictionary keys don't already exist with the event name, add them
            if (_handlers.ContainsKey(eventName))
                _handlers.Add(eventName, new List<Type>());

            //if handler already exist of handler type throw exception
            //here handler type would be equal to event name
            if (_handlers[eventName].Any(s=>s.GetType() == handlerType))
            {
                throw new ArgumentException(
                    $"Handler type {handlerType.Name} already is registered for '{ eventName }'", nameof(handlerType));
            }

            //assign handler and add it to the list of types
            _handlers[eventName].Add(handlerType);

            ////consuming message
            StartBasicConsume<T>();
        }

        private void StartBasicConsume<T>() where T : Event
        {
            var factory = new ConnectionFactory()
            {
                HostName = "localhost",
                DispatchConsumersAsync = true   //making our consumer async
            };

            var connection = factory.CreateConnection();
            var channel = connection.CreateModel();
            var eventName = typeof(T).Name;

            channel.QueueDeclare(eventName, false, false, false, null);

            var consumer = new AsyncEventingBasicConsumer(channel);

            //creating delegate - a placeholder for an event, a pointer to a method
            //meaning as soon as message comes into queue, this "Consumer_Received" will kick off
            consumer.Received += Consumer_Received;

            channel.BasicConsume(eventName, true, consumer);
            
        }

        private async Task Consumer_Received(object sender, BasicDeliverEventArgs e)
        {
            //at this point message has come into our queue, so we need a way to pick up that msg and 
            //convert it to an actual object and send it  through our bus to whomever is handling that event

            var eventName = e.RoutingKey;
            var message = Encoding.UTF8.GetString(e.Body.ToArray());

            //we've grabbed a hold of our message in the queue, now we have to process/kick of event handler
            //in the try catch 

            try
            {
                //know wich handler is subscribed to this type of event and then do all the work in background
                await ProcessEvent(eventName, message).ConfigureAwait(false);
            }
            catch (Exception ex)
            {

                throw;
            }

        }

        private async Task ProcessEvent(string eventName, string message)
        {
            //here we dinamically create handler based on our handler type in dictionary of handlers
            //and then invoke the event handler for that type of event
            //this handles any handler, not just specific type of handler

            //we can have multiple subscribers
            if(_handlers.ContainsKey(eventName))
            {
                var subscriptions = _handlers[eventName]; //because there can be multiple subscribers to this event
                foreach(var subscription in subscriptions)
                {
                    //creating handler; dinamically creating instance of Type -> this is for generics
                    var handler = Activator.CreateInstance(subscription);
                    if (handler == null) continue;   //continue looping until found

                    //now we can loop through our events
                    var eventType = _eventTypes.SingleOrDefault(t => t.Name == eventName);

                    var @event = JsonConvert.DeserializeObject(message, eventType);

                    var concreteType = typeof(IEventHandler<>).MakeGenericType(eventType);

                    //invoking main method - generic - it will handle any situtation
                    //inovking 'Handle' method of concreteType
                    //this will use generics to kick of handle method inside our handler and passing
                    //it the event
                    await (Task)concreteType.GetMethod("Handle").Invoke(handler, new object[] { @event });
                    //this doees the main work of routing to the right handler
                }
            }
        }
    }
}
