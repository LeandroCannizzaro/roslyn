// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using Microsoft.CodeAnalysis.Editor.Commands;
using Microsoft.CodeAnalysis.Editor.Host;
using Microsoft.CodeAnalysis.Editor.Shared.Extensions;
using Microsoft.CodeAnalysis.FindSymbols;
using Microsoft.CodeAnalysis.Internal.Log;
using Microsoft.CodeAnalysis.Shared.TestHooks;
using Microsoft.CodeAnalysis.Text;
using Microsoft.VisualStudio.Text;
using Roslyn.Utilities;

namespace Microsoft.CodeAnalysis.Editor.Implementation.FindReferences
{
    [ExportCommandHandler(PredefinedCommandHandlerNames.FindReferences, ContentTypeNames.RoslynContentType)]
    internal class FindReferencesCommandHandler : ICommandHandler<FindReferencesCommandArgs>
    {
        private readonly IEnumerable<IReferencedSymbolsPresenter> _synchronousPresenters;
        private readonly IEnumerable<IAsyncFindReferencesPresenter> _asynchronousPresenters;

        private readonly IWaitIndicator _waitIndicator;
        private readonly IAsynchronousOperationListener _asyncListener;

        [ImportingConstructor]
        internal FindReferencesCommandHandler(
            IWaitIndicator waitIndicator,
            [ImportMany] IEnumerable<IReferencedSymbolsPresenter> synchronousPresenters,
            [ImportMany] IEnumerable<IAsyncFindReferencesPresenter> asynchronousPresenters,
            [ImportMany] IEnumerable<Lazy<IAsynchronousOperationListener, FeatureMetadata>> asyncListeners)
        {
            Contract.ThrowIfNull(waitIndicator);
            Contract.ThrowIfNull(synchronousPresenters);
            Contract.ThrowIfNull(asynchronousPresenters);

            _waitIndicator = waitIndicator;
            _synchronousPresenters = synchronousPresenters;
            _asynchronousPresenters = asynchronousPresenters;
            _asyncListener = new AggregateAsynchronousOperationListener(
                asyncListeners, FeatureAttribute.FindReferences);
        }

        internal void FindReferences(ITextSnapshot snapshot, int caretPosition)
        {
            _waitIndicator.Wait(
                title: EditorFeaturesResources.Find_References,
                message: EditorFeaturesResources.Finding_references,
                action: context =>
            {
                Document document = snapshot.GetOpenDocumentInCurrentContextWithChanges();
                if (document != null)
                {
                    var service = document.Project.LanguageServices.GetService<IFindReferencesService>();
                    if (service != null)
                    {
                        using (Logger.LogBlock(FunctionId.CommandHandler_FindAllReference, context.CancellationToken))
                        {
                            if (!service.TryFindReferences(document, caretPosition, context))
                            {
                                foreach (var presenter in _synchronousPresenters)
                                {
                                    presenter.DisplayResult(document.Project.Solution, SpecializedCollections.EmptyEnumerable<ReferencedSymbol>());
                                    return;
                                }
                            }
                        }
                    }
                }
            }, allowCancel: true);
        }

        public CommandState GetCommandState(FindReferencesCommandArgs args, Func<CommandState> nextHandler)
        {
            return nextHandler();
        }

        public void ExecuteCommand(FindReferencesCommandArgs args, Action nextHandler)
        {
            var caretPosition = args.TextView.GetCaretPoint(args.SubjectBuffer) ?? -1;

            if (caretPosition < 0)
            {
                nextHandler();
                return;
            }

            var snapshot = args.SubjectBuffer.CurrentSnapshot;



            FindReferences(snapshot, caretPosition);
        }
    }
}
