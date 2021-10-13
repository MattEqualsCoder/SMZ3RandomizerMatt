﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Text.Json.Serialization;

namespace Randomizer.SMZ3.Tracking.Vocabulary
{
    /// <summary>
    /// Represents a string whose value is picked randomly from a collection of
    /// strings.
    /// </summary>
    [JsonConverter(typeof(SchrodingersStringConverter))]
    public class SchrodingersString : Collection<SchrodingersString.Item>
    {
        private static readonly Random s_random = new();

        /// <summary>
        /// Initializes a new instance of the <see cref="SchrodingersString"/>
        /// class that is empty.
        /// </summary>
        public SchrodingersString()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="SchrodingersString"/>
        /// class with the specified items.
        /// </summary>
        /// <param name="items">The items to add.</param>
        public SchrodingersString(IEnumerable<Item> items)
        {
            foreach (var item in items)
                Add(item);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="SchrodingersString"/>
        /// class with the specified items.
        /// </summary>
        /// <param name="items">The items to add.</param>
        public SchrodingersString(params Item[] items)
        {
            foreach (var item in items)
                Add(item);
        }

        /// <summary>
        /// Returns a random string from the specified possibilities.
        /// </summary>
        /// <param name="value"></param>
        public static implicit operator string?(SchrodingersString? value)
            => value?.ToString();

        /// <summary>
        /// Determines whether the specified text is among the possibilities for
        /// this string.
        /// </summary>
        /// <param name="text">The string to test.</param>
        /// <param name="stringComparison">
        /// The type of comparison method to use.
        /// </param>
        /// <returns>
        /// <see langword="true"/> if <paramref name="text"/> is among the
        /// possibilities, otherwise, <see langword="false"/>.
        /// </returns>
        public bool Contains(string text, StringComparison stringComparison)
        {
            return Items.Any(x => x.Text.Equals(text, stringComparison));
        }

        /// <summary>
        /// Returns a random string from the possibilities.
        /// </summary>
        /// <returns>A random string from the possibilities.</returns>
        public override string? ToString() => Random(s_random)?.Text;

        /// <summary>
        /// Replaces placeholders in the string with the specified values.
        /// </summary>
        /// <param name="args">
        /// A collection of objects to format the string with.
        /// </param>
        /// <returns>The formatted string, or <c>null</c> if</returns>
        public string? Format(params object[] args)
        {
            var value = ToString();
            return value != null ? string.Format(value, args) : null;
        }

        /// <summary>
        /// Picks a phrase at random, taking into account any weights associated
        /// with the phrases.
        /// </summary>
        /// <param name="random">The random number generator to use.</param>
        /// <returns>
        /// A random phrase from the phrase set, or <c>null</c> if the phrase
        /// set contains no items.
        /// </returns>
        protected Item? Random(Random random)
        {
            if (Items.Count == 0)
                return null;

            var target = random.NextDouble() * GetTotalWeight();
            foreach (var item in Items)
            {
                if (target < item.Weight)
                    return item;

                target -= item.Weight;
            }

            throw new Exception("This code should not be reachable.");
        }

        private double GetTotalWeight() => Items.Sum(x => x.Weight);

        /// <summary>
        /// Represents one possibility of a <see cref="SchrodingersString"/>.
        /// </summary>
        [JsonConverter(typeof(SchrodingersStringItemConverter))]
        [DebuggerDisplay("{Text} Weight = {Weight}")]
        public class Item
        {
            /// <summary>
            /// The weight of items that do not have an explicit weight assigned
            /// to them.
            /// </summary>
            public const double DefaultWeight = 1.0d;

            /// <summary>
            /// Initializes a new instance of the <see cref="Item"/> class with
            /// the specified text and the default weight of 1.
            /// </summary>
            /// <param name="text">The text.</param>
            public Item(string text) : this(text, DefaultWeight)
            {
            }

            /// <summary>
            /// Initializes a new instance of the <see cref="Item"/> class with
            /// the specified text and weight.
            /// </summary>
            /// <param name="text">The text.</param>
            /// <param name="weight">
            /// The weight for the item, based on a default of 1.
            /// </param>
            public Item(string text, double weight)
            {
                if (weight < 0)
                    throw new ArgumentOutOfRangeException(nameof(weight), weight, "Weight cannot be less than zero.");

                Text = text;
                Weight = weight;
            }

            /// <summary>
            /// Gets a string.
            /// </summary>
            public string Text { get; }

            /// <summary>
            /// Gets the weight associated with the item.
            /// </summary>
            [DefaultValue(DefaultWeight)]
            public double Weight { get; }

            public static implicit operator string(Item phrase) => phrase.Text;

            public static implicit operator Item(string text) => new(text);

            public override int GetHashCode() => Text.GetHashCode();

            public override string ToString() => Text;
        }
    }
}
