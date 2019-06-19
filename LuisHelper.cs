// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.AI.Luis;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;

namespace Microsoft.BotBuilderSamples
{
    public static class LuisHelper
    {
        public static async Task<BookingDetails> ExecuteLuisQuery(IConfiguration configuration, ILogger logger, ITurnContext turnContext, CancellationToken cancellationToken)
        {
            var bookingDetails = new BookingDetails();

            try
            {
                // Create the LUIS settings from configuration.
                var luisApplication = new LuisApplication(
                    configuration["LuisAppId"],
                    configuration["LuisAPIKey"],
                    "https://" + configuration["LuisAPIHostName"]
                );

                var recognizer = new LuisRecognizer(luisApplication);

                // The actual call to LUIS
                var recognizerResult = await recognizer.RecognizeAsync(turnContext, cancellationToken);

                var (intent, score) = recognizerResult.GetTopScoringIntent();
                if (intent == "Book_flight")
                {
                    // We need to get the result from the LUIS JSON which at every level returns an array.
                    bookingDetails.Destination = recognizerResult.Entities["To"]?.FirstOrDefault()?["Airport"]?.FirstOrDefault()?.FirstOrDefault()?.ToString();

                    if (bookingDetails.Destination == null)
                        bookingDetails.Destination = recognizerResult.GetEntity<string>("To");                

                    bookingDetails.Origin = recognizerResult.Entities["From"]?.FirstOrDefault()?["Airport"]?.FirstOrDefault()?.FirstOrDefault()?.ToString();

                    if (bookingDetails.Origin == null)
                        bookingDetails.Origin = recognizerResult.GetEntity<string>("From");

                    // This value will be a TIMEX. And we are only interested in a Date so grab the first result and drop the Time part.
                    // TIMEX is a format that represents DateTime expressions that include some ambiguity. e.g. missing a Year.
                    bookingDetails.TravelDate = recognizerResult.Entities["datetime"]?.FirstOrDefault()?["timex"]?.FirstOrDefault()?.ToString().Split('T')[0];
                } 
            }
            catch (Exception e)
            {
                logger.LogWarning($"LUIS Exception: {e.Message} Check your LUIS configuration.");
            }

            return bookingDetails;
        }

        public static T GetEntity<T>(this RecognizerResult luisResult, string entityKey, string valuePropertyName = "text")
        {
            if (luisResult != null)
            {
                //// var value = (luisResult.Entities["$instance"][entityKey][0]["text"] as JValue).Value;
                var data = luisResult.Entities as IDictionary<string, JToken>;

                if (data.TryGetValue("$instance", out JToken value))
                {
                    var entities = value as IDictionary<string, JToken>;
                    if (entities.TryGetValue(entityKey, out JToken targetEntity))
                    {
                        var entityArray = targetEntity as JArray;
                        if (entityArray.Count > 0)
                        {
                            var values = entityArray[0] as IDictionary<string, JToken>;
                            if (values.TryGetValue(valuePropertyName, out JToken textValue))
                            {
                                var text = textValue as JValue;
                                if (text != null)
                                {
                                    return (T)text.Value;
                                }
                            }
                        }
                    }
                }
            }

            return default(T);
        }
    }
}
