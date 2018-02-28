﻿using System;
using System.Collections;
using Silphid.Extensions;
using Silphid.Loadzup;
using Silphid.Requests;
using Silphid.Tweenzup;
using UniRx;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Silphid.Showzup
{
    public static class IBinderExtensions
    {
        public static void AddTo(this IDisposable This, IBinder binder) => binder.Add(This);
        
        public static void BindActive(this IBinder This, IObservable<bool> source, GameObject target)
        {
            if (target != null)
                source.Subscribe(target.SetActive).AddTo(This);
        }

        public static void Bind<T>(this IBinder This, IObservable<T> source, IReactiveProperty<T> target) =>
            source.Subscribe(x => target.Value = x).AddTo(This);

        public static void Bind<T>(this IBinder This, IReactiveProperty<T> source, IReactiveProperty<T> target) where T : class =>
            This.Bind<T, T>(source, target);
        
        public static void Bind<TSource, TTarget>(this IBinder This, IReactiveProperty<TSource> source, IReactiveProperty<TTarget> target)
            where TSource : class
            where TTarget : class
        {
            if (source == null || target == null)
                return;

            bool isUpdating = false;

            source
                .Where(_ => !isUpdating)
                .Subscribe(x =>
                {
                    isUpdating = true;
                    target.Value = x as TTarget;
                    isUpdating = false;
                })
                .AddTo(This);

            target
                .Where(_ => !isUpdating)
                .Subscribe(x =>
                {
                    isUpdating = true;
                    source.Value = x as TSource;
                    isUpdating = false;
                })
                .AddTo(This);
        }

        public static void Bind<T>(this IBinder This, IObservable<T> source, IPresenter target) =>
            source.Subscribe(x => target.Present(x).SubscribeAndForget()).AddTo(This);

        public static void Bind(this IBinder This, Button source, IRequest target) =>
            source?.OnClickAsObservable().Subscribe(_ => source.Send(target)).AddTo(This);

        public static void Bind<TRequest>(this IBinder This, Button source) where TRequest : IRequest, new() =>
            source.OnClickAsObservable().Subscribe(_ => source.Send<TRequest>()).AddTo(This);

        public static void Bind<T>(this IBinder This, IReadOnlyReactiveCollection<T> source, ListControl target) =>
            This.Bind(source.ObserveCurrentAddRemove(), target);

        public static void Bind<T>(this IBinder This, IObservable<CollectionAddRemoveEvent<T>> source, ListControl target) =>
            source
                .Subscribe(x =>
                {
                    if (x.IsAdded)
                        target.Add(x.Value).SubscribeAndForget();
                    else
                        target.Remove(x.Value);
                })
                .AddTo(This);
        
        public static void Bind(this IBinder This, string source, Text target)
        {
            if (target != null)
                target.text = source;
        }

        public static ICompletable BindAsCompletable(this IBinder This, object source, IPresenter target)
        {
            if (source == null || target == null)
                return Completable.Empty();

            return target
                .Present(source)
                .AutoDetach()
                .AsCompletable();
        }

        public static void Bind(this IBinder This, object source, IPresenter target)
        {
            if (source != null)
                target?
                    .Present(source)
                    .AutoDetach()
                    .Subscribe()
                    .AddTo(This);
        }

        public static void Bind(this IBinder This, IEnumerable source, ListControl target)
        {
            if (target != null)
                This.BindAsCompletable(source, target)
                    .Subscribe()
                    .AddTo(This);
        }

        public static ICompletable BindAsCompletable(this IBinder This, IEnumerable source, ListControl target) =>
            target
                ?.Present(source)
                .AsCompletable()
                .AutoDetach()
            ?? Completable.Empty();

        public static void Bind(this IBinder This, string source, Image target, bool keepVisible = false, float? fadeDuration = null) =>
            This.Bind(new Uri(source), target, keepVisible, fadeDuration);

        public static void Bind(this IBinder This, Uri source, Image target, bool keepVisible = false, float? fadeDuration = null)
        {
            if (target != null)
                This.BindAsCompletable(source, target, false, null, keepVisible, fadeDuration)
                    .Subscribe()
                    .AddTo(This);
        }

        public static ICompletable BindAsCompletable(this IBinder This, string source, Image target, bool isOptional = false,
            Loadzup.Options options = null,
            bool keepVisible = false, float? fadeDuration = null)
        {
            if (source == null || target == null)
                return Completable.Empty();
            
            return This.BindAsCompletable(new Uri(source), target, isOptional, options, keepVisible, fadeDuration);
        }

        public static ICompletable BindAsCompletable(this IBinder This, Uri source, Image target, bool isOptional = false,
            Loadzup.Options options = null,
            bool keepVisible = false, float? fadeDuration = null)
        {
            if (target == null)
                return Completable.Empty();

            if (source == null)
            {
                if (isOptional)
                    return Completable.Empty();

                return Completable.Throw(
                    new BindException(
                        $"Cannot bind required image {target.gameObject.name} in view {This.View.GameObject.name} to null Uri."));
            }

            if (fadeDuration != null)
                target.color = Color.clear;
            else
                target.enabled = keepVisible;

            return This.Loader
                .With(This.DefaultImageHttpCachePolicy)
                .Load<DisposableSprite>(source, options)
                .Catch<DisposableSprite, Exception>(ex =>
                    Observable.Throw<DisposableSprite>(
                        new BindException(
                            $"Failed to load image {target.gameObject.name} in view {This.View.GetType().Name} from {source}", ex)))
                .Do(x =>
                {
                    if (target.IsDestroyed())
                    {
                        if (source.Scheme == Scheme.Http || source.Scheme == Scheme.Https ||
                            source.Scheme == Scheme.StreamingAsset || source.Scheme == Scheme.StreamingFile)
                            x.Dispose();
                        return;
                    }

                    target.sprite = x.Sprite;
                    target.enabled = true;

                    if (fadeDuration != null)
                        Observable.NextFrame()
                            .Then(_ => target.TweenColorTo(Color.white, fadeDuration.Value))
                            .SubscribeAndForget()
                            .AddTo(This);

                    if (source.Scheme == Scheme.Http || source.Scheme == Scheme.Https || source.Scheme == Scheme.StreamingAsset
                        || source.Scheme == Scheme.StreamingFile)
                        x.AddTo(This);
                })
                .AutoDetach()
                .AsCompletable();
        }

        public static ICompletable BindAsCompletable(this IBinder This, RawImage image, Uri uri, bool isOptional = false,
            Loadzup.Options options = null,
            bool keepVisible = false, float? fadeDuration = null)
        {
            if (image == null)
                return Completable.Empty();

            if (uri == null)
            {
                if (isOptional)
                    return Completable.Empty();

                return Completable.Throw(
                    new BindException(
                        $"Cannot bind required image {image.gameObject.name} in view {This.View.GameObject.name} to null Uri."));
            }

            if (fadeDuration != null)
                image.color = Color.clear;
            else
                image.enabled = keepVisible;

            return This.Loader
                .With(This.DefaultImageHttpCachePolicy)
                .Load<Texture2D>(uri, options)
                .Catch<Texture2D, Exception>(ex => Observable
                    .Throw<Texture2D>(new BindException(
                        $"Failed to load image {image.gameObject.name} in view {This.View.GetType().Name} from {uri}", ex)))
                .Do(x =>
                {
                    if (image.IsDestroyed())
                    {
                        if (uri.Scheme == Scheme.Http || uri.Scheme == Scheme.Https ||
                            uri.Scheme == Scheme.StreamingAsset || uri.Scheme == Scheme.StreamingFile)
                            Object.Destroy(x);
                        return;
                    }

                    image.texture = x;
                    image.enabled = true;

                    if (fadeDuration != null)
                        Observable.NextFrame()
                            .Then(_ => image.TweenColorTo(Color.white, fadeDuration.Value))
                            .SubscribeAndForget()
                            .AddTo(This);

                    if (uri.Scheme == Scheme.Http || uri.Scheme == Scheme.Https ||
                        uri.Scheme == Scheme.StreamingAsset || uri.Scheme == Scheme.StreamingFile)
                    {
                        Disposable
                            .Create(() => Object.Destroy(x))
                            .AddTo(This);
                    }
                })
                .AutoDetach()
                .AsCompletable();
        }
    }
}