# Method Binding Diagnostics

## Undefined Name

```sobakasu
on Interact() {
  Foo.Bar();
}
```

Expected diagnostic: `SBK2002`

## Undefined Member

```sobakasu
on Interact() {
  Debug.NoSuchMethod();
}
```

Expected diagnostic: `SBK2003`

## Invalid Argument Count

```sobakasu
on Interact() {
  Debug.Log();
}
```

Expected diagnostic: `SBK2004`

## No Matching Overload

```sobakasu
on Interact() {
  Debug.Log([1]);
}
```

Expected diagnostic: `SBK2013`
