# Demo animal photos

These are real photos of real pets, used only for the demo animals that `DataSeeder` loads when
`Seed:DemoData=true` (development and staging). Production never loads them. The animals' names,
breeds and stories in PawConnect are fictional.

Each file is cropped to 4:3 and compressed. They're embedded in the Infrastructure assembly and
attached to the matching demo animal (by name) the first time the app starts.

| File | Original | Source | Licence |
|---|---|---|---|
| `biscuit.jpg` | `mix/Masala.jpg` | [Dog API images](https://github.com/jigsawpieces/dog-api-images), submitted by the dog's owner | GPL-3.0 (repository licence) |
| `nala.jpg` | `labrador/Luke.jpg` | Dog API images, submitted by the dog's owner | GPL-3.0 |
| `duke.jpg` | `bullterrier-staffordshire/caesar.jpg` | Dog API images, submitted by the dog's owner | GPL-3.0 |
| `rocky.jpg` | `mastiff-english/4.jpg` | Dog API images, submitted by the dog's owner | GPL-3.0 |
| `luna.jpg` | `terrier-russell/iguet3.jpg` | Dog API images, submitted by the dog's owner | GPL-3.0 |
| `whiskers.jpg` | `cat_photos/548c8aa491a811e1be6a12313820455d_7.png` | [max-mapper/cats](https://github.com/max-mapper/cats), photographed by Max Ogden | BSD |
| `pumpkin.jpg` | `cat_photos/0c375608c71811e1b10e123138105d6b_7.png` | max-mapper/cats, photographed by Max Ogden | BSD |
| `milo.jpg` | `cat_photos/66250dd8be6011e1aebc1231381b647a_7.png` | max-mapper/cats, photographed by Max Ogden | BSD |
| `mochi.jpg` | `cat_photos/3ba670686e7111e181bd12313817987b_7.png` | max-mapper/cats, photographed by Max Ogden | BSD |

Only owner-submitted Dog API photos were used, not the ones from the Stanford Dogs/ImageNet set
in the same repository.

For a real Hope & Paws deployment, replace these with the shelter's own photos. Admins can upload
photos per animal under **Admin → Animals → Edit**.
