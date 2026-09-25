import { defineConfig } from 'astro/config';
import starlight from '@astrojs/starlight';
import sobakasuGrammar from '../Packages/com.skytomo221.sobakasu/Tools~/VSCodeExtension/syntaxes/sobakasu.tmLanguage.json' with { type: 'json' };

const sobakasuLanguage = { ...sobakasuGrammar, name: 'sobakasu' };

export default defineConfig({
  site: 'https://skytomo221.com',
  base: '/Sobakasu',
  output: 'static',
  integrations: [
    starlight({
      title: 'Sobakasu Documentation',
      description: 'Sobakasu is a Udon-first language and compiler for VRChat.',
      favicon: '/favicon.svg',
      expressiveCode: {
        shiki: {
          langs: [sobakasuLanguage],
        },
      },
      defaultLocale: 'ja',
      locales: {
        ja: {
          label: '日本語',
          lang: 'ja',
        },
        en: {
          label: 'English',
          lang: 'en',
        },
        ko: {
          label: '한국어',
          lang: 'ko',
        },
        'zh-cn': {
          label: '简体中文',
          lang: 'zh-CN',
        },
      },
      sidebar: [
        {
          label: 'ガイド',
          items: [{ autogenerate: { directory: 'guide' } }],
        },
        {
          label: '言語',
          items: [{ autogenerate: { directory: 'language' } }],
        },
        {
          label: 'リファレンス',
          items: [{ autogenerate: { directory: 'reference' } }],
        },
        {
          label: 'サンプル',
          items: [{ autogenerate: { directory: 'samples' } }],
        },
      ],
      social: [{ icon: 'github', label: 'GitHub', href: 'https://github.com/skytomo221/Sobakasu' }],
    }),
  ],
});
