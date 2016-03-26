# Gramma.LanguageModel.Greek.TrainingSources
This library provides training source input for the Greek language in order to be used by [Gramma.Inference](https://github.com/grammophone/Gramma.Inference), a part-of-speech tagging and lemmatization framework.

From the [Perseus project](http://www.perseus.tufts.edu/hopper/opensource/download), it adapts a training source for `TaggedWordForm` items using class `PerseusTaggedWordTrainingSource` as well as a source of `TaggedSentence` items using class `PerseusSentenceTrainingSource`, which draws data provided by the [Perseus Ancient Greek and Latin Dependency Treebank](https://perseusdl.github.io/treebank_data/). 

From the [Tischendorf's New Testament with morphological tags](https://github.com/morphgnt/tischendorf-data/tree/master/word-per-line/2.7), it adapts a training source for `TaggedSentence` items via class `TischendorfSentenceTrainingSource`. 

From the Rahlfs edition of Septuagint Old Testament, using material provided by the [Center for Computer
Analysis of Texts (CCAT) at the University of Pennsylvania](http://ccat.sas.upenn.edu/gopher/text/religion/biblical/lxxmorph/0-readme.txt) and correlated with public domain data to add the missing punctuation, the class `LXXSentenceTrainingSource` draws `TaggedSentence` items.

The above are summarized in the following UML diagram.

![Greek training source classes](http://s10.postimg.org/lfgq5gmt5/Greek_training_sources.png)

The actual files being used from the above sources are included inside the 'Training sets' directory of project [Gramma.TrainingApplication](https://github.com/grammophone/Gramma.TrainingApplication).

This project requires that the following projects be in sibling directories:
* [Gramma.BetaImport](https://github.com/grammophone/Gramma.BetaImport)
* [Gramma.GenericContentModel](https://github.com/grammophone/Gramma.GenericContentModel)
* [Gramma.LanguageModel](https://github.com/grammophone/Gramma.LanguageModel)

