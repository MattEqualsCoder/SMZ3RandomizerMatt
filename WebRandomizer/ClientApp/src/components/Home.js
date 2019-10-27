import React, { Component } from 'react';
const ReactMarkdown = require('react-markdown');
const homeMarkdown =
`
### Super Metroid and A Link to the Past Crossover Randomizer - Multiworld Beta
Welcome to the beta version of the Multiworld-edition of the randomizer.

First of all, please go to the [Instruction](/instructions) page for information about how to set things up and get started.

To start a new multiworld session, go to the [Create Randomized Game](/randomizer) page.

*Please note that this is still in beta and being under constant developement, so things might not work or the site might have some downtime.*

Looking for people to play with, need support or just anything else related to the randomizer, please join the [Discord](https://discord.gg/PMKcDPQ).

`;

export class Home extends Component {
  static displayName = Home.name;

  render () {
    return (
      <ReactMarkdown source={homeMarkdown} />
    );
  }
}
