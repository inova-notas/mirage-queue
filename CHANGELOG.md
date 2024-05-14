# Change Log

All notable changes to this project will be documented in this file. See [versionize](https://github.com/versionize/versionize) for commit guidelines.

<a name="1.3.0"></a>
## [1.3.0](https://www.github.com/inova-notas/mirage-queue/releases/tag/v1.3.0) (2024-05-14)

### Features

* Auto run migration when call the UseMirageQueue extension method ([74ef7f1](https://www.github.com/inova-notas/mirage-queue/commit/74ef7f17189a2fd5e43d1693defd173c48bcbdba))

<a name="1.2.0"></a>
## [1.2.0](https://www.github.com/inova-notas/mirage-queue/releases/tag/v1.2.0) (2024-05-14)

### Features

* add scheduled message support ([65b803b](https://www.github.com/inova-notas/mirage-queue/commit/65b803be4e7d0ea069cf028f2ced54812e2decfb))

### Bug Fixes

* changed to not track entity message and update using raw sql ([4129ede](https://www.github.com/inova-notas/mirage-queue/commit/4129ede7ec8a3a4b8501a262d5ac90ed986e2c79))
* not use db context concurrently when update OutBoundMessage Entity ([968bf9f](https://www.github.com/inova-notas/mirage-queue/commit/968bf9fa8073e662d93911f12f7e26f6938b7608))

<a name="1.1.1"></a>
## [1.1.1](https://www.github.com/Beeposts/mirage-queue/releases/tag/v1.1.1) (2023-12-28)

### Bug Fixes

* changed select message query to use limit parameter ([3010980](https://www.github.com/Beeposts/mirage-queue/commit/3010980e2572b565e2c488d61b20d34b2a8e1b47))

<a name="1.1.0"></a>
## [1.1.0](https://www.github.com/Beeposts/mirage-queue/releases/tag/v1.1.0) (2023-12-27)

### Features

* changed to process each message on a separate worker ([9837e8f](https://www.github.com/Beeposts/mirage-queue/commit/9837e8f355c74351f9ca52fa722b8342c4699897))

<a name="1.0.0"></a>
## [1.0.0](https://www.github.com/Beeposts/mirage-queue/releases/tag/v1.0.0) (2023-12-27)

### Features

* added multiple workers to process in/out messages ([b237d28](https://www.github.com/Beeposts/mirage-queue/commit/b237d28c2d92a7a5bcb68a7aac229c9fb4d2b228))
* created dispatcher ([4973154](https://www.github.com/Beeposts/mirage-queue/commit/4973154b33dbe8788990797e257b105b9dd8c561))
* created message structure ([133c181](https://www.github.com/Beeposts/mirage-queue/commit/133c1816d0b833d394ffb559b4c941a0b6401ba6))
* created outbound message dispatcher method ([074baa4](https://www.github.com/Beeposts/mirage-queue/commit/074baa425a0e5b7023e9378df6977217e48580a3))
* created postgres structure ([63c6eba](https://www.github.com/Beeposts/mirage-queue/commit/63c6eba45d2a66d9d41e7366c73d7afe61b9aab8))
* creted base structure for pub/sub with postgres ([2a141df](https://www.github.com/Beeposts/mirage-queue/commit/2a141df49583bfb3dc2a6cf61d33f0592373d9bb))

### Bug Fixes

* changed the solution/project folder name ([97f307c](https://www.github.com/Beeposts/mirage-queue/commit/97f307c2bb7cf845fd00d64ac578dcf7bafe3ade))
* check for existing consumer before add it ([b545087](https://www.github.com/Beeposts/mirage-queue/commit/b545087cca7eea01ab9d9ebe9aebe3f5f5328725))
* moved solution to src folder ([2447fd8](https://www.github.com/Beeposts/mirage-queue/commit/2447fd8babfe2064b0a9b945acff59a43aebb244))
* Removed OS/IDE specfic folder ([09be790](https://www.github.com/Beeposts/mirage-queue/commit/09be790da8b10817b13194ec45721210d773e2ba))

